using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;

namespace SimpleBench;

internal sealed class GpuBenchmarkHandler : IDisposable {
	private readonly Context _context;

	public GpuBenchmark[] Benchmarks { get; }

	public GpuBenchmarkHandler(HashSet<string> accelerators) {
		_context = Context.CreateDefault();

		Device[] devices = [
			.. accelerators.Count == 0
				? _context.GetPreferredDevices(false, false)
				: _context.Devices.Where(device => accelerators.Contains(device.Name))
		];

		Benchmarks = new GpuBenchmark[devices.Length];
		for (int i = 0; i < devices.Length; i++)
			Benchmarks[i] = new GpuBenchmark(_context, devices[i], i);
	}

	public void Dispose() {
		foreach (GpuBenchmark benchmark in Benchmarks)
			benchmark.Dispose();
		_context.Dispose();
	}
}

internal sealed class GpuBenchmark : Benchmark, IDisposable {
	private const int BufferLength = Gibibyte / sizeof(float);

	private readonly Accelerator _accelerator;

	private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _setupKernel;

	private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _triadKernel;

	private readonly Action<Index1D, ArrayView<float>> _resetKernel;

	private MemoryBuffer1D<float, Stride1D.Dense>? _bufferA;

	private MemoryBuffer1D<float, Stride1D.Dense>? _bufferB;

	private MemoryBuffer1D<float, Stride1D.Dense>? _bufferC;

	public override string Name { get; }

	public GpuBenchmark(Context context, Device device, int deviceIndex) {
		_accelerator = device.CreateAccelerator(context);

		_setupKernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(SetupKernel);
		_triadKernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(TriadKernel);
		_resetKernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>>(ResetKernel);

		Name = $"GPU_{device.Name}";
	}

	private static void SetupKernel(Index1D index, ArrayView<float> b, ArrayView<float> c) {
		b[index] = 1;
		c[index] = 2;
	}

	private static void TriadKernel(Index1D index, ArrayView<float> a, ArrayView<float> b, ArrayView<float> c) {
		a[index] = b[index] + 3 * c[index];
	}

	private static void ResetKernel(Index1D index, ArrayView<float> a) {
		a[index] = 0;
	}

	public override void Init() {
		if (State != InitState.NotInitialized)
			return;
		State = InitState.Initializing;

		_bufferA = _accelerator.Allocate1D<float>(BufferLength);
		_bufferB = _accelerator.Allocate1D<float>(BufferLength);
		_bufferC = _accelerator.Allocate1D<float>(BufferLength);

		_setupKernel(BufferLength, _bufferB.View, _bufferC.View);
		_accelerator.Synchronize();

		State = InitState.Initialized;
	}

	protected override double RunBenchmark() {
		if (_bufferA is null || _bufferB is null || _bufferC is null)
			throw new NotInitializedException(Name);

		_resetKernel(BufferLength, _bufferA.View);
		_accelerator.Synchronize();

		Stopwatch sw = new();
		sw.Start();

		_triadKernel(BufferLength, _bufferA.View, _bufferB.View, _bufferC.View);
		_accelerator.Synchronize();

		sw.Stop();
		return sw.Elapsed.TotalSeconds;
	}

	public void Dispose() {
		_bufferC?.Dispose();
		_bufferB?.Dispose();
		_bufferA?.Dispose();
		_accelerator.Dispose();
	}
}
