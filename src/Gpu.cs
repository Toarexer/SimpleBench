using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;

namespace SimpleBench;

/// Creates and manages the resources of <see cref="GpuBenchmark"/> objects.
internal sealed class GpuBenchmarkHandler : IDisposable {
	/// The <see cref="Context"/> used for getting the devices to be used and creating the <see cref="Accelerator"/> objects.
	private readonly Context _context;

	/// The <see cref="GpuBenchmark"/> that will be run sequentially.
	public GpuBenchmark[] Benchmarks { get; }

	/// <summary>Creates a new instance of a <see cref="GpuBenchmarkHandler"/>.</summary>
	/// <param name="accelerators">A collection IDs from <see cref="DeviceInfo"/> objects that determine which devices will be used. If left empty the preferred devices are used.</param>
	public GpuBenchmarkHandler(HashSet<string> accelerators) {
		_context = Context.CreateDefault();

		DeviceInfo[] devices = [
			.. accelerators.Count == 0
				? Utils.GetPreferredDevices(_context)
				: Utils.GetAvailableDevices(_context).Where(device => accelerators.Contains(device.Id))
		];

		Benchmarks = new GpuBenchmark[devices.Length];
		for (int i = 0; i < devices.Length; i++)
			Benchmarks[i] = new GpuBenchmark(_context, devices[i]);
	}

	public void Dispose() {
		foreach (GpuBenchmark benchmark in Benchmarks)
			benchmark.Dispose();
		_context.Dispose();
	}
}

/// Represents an accelerator benchmark that can be initialized and ran to execute a STREAM Triad benchmark to measure how many times they can be completed per second.
internal sealed class GpuBenchmark : Benchmark, IDisposable {
	/// 1 GiB worth of <see cref="float"/> values
	private const int BufferLength = Gibibyte / sizeof(float);

	/// The accelerator to use for the STREAM Triad benchmark.
	private readonly Accelerator _accelerator;

	/// The kernel used for the initialization of <see cref="_bufferB"/> and <see cref="_bufferC"/>.
	private readonly Action<Index1D, ArrayView<float>, ArrayView<float>> _setupKernel;

	/// The STREAM Triad kernel.
	private readonly Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> _triadKernel;

	/// The kernel used for the resetting of <see cref="_bufferA"/>.
	private readonly Action<Index1D, ArrayView<float>> _resetKernel;

	/// Buffer A of the STREAM Triad benchmark.
	private MemoryBuffer1D<float, Stride1D.Dense>? _bufferA;

	/// Buffer B of the STREAM Triad benchmark.
	private MemoryBuffer1D<float, Stride1D.Dense>? _bufferB;

	/// Buffer C of the STREAM Triad benchmark.
	private MemoryBuffer1D<float, Stride1D.Dense>? _bufferC;

	public override string Name { get; }

	/// <summary>
	/// Creates a new instance of a <see cref="GpuBenchmark"/>.
	/// Also creates an accelerator using the provided <see cref="Context"/> and <see cref="DeviceInfo"/>.
	/// After the accelerator is created the kernels used for setup and benchmarking are loaded.
	/// </summary>
	/// <param name="context">The context to use for the accelerator creation.</param>
	/// <param name="info">Identifies the <see cref="Device"/> to use for the accelerator creation.</param>
	public GpuBenchmark(Context context, DeviceInfo info) {
		_accelerator = info.Device.CreateAccelerator(context);

		_setupKernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(SetupKernel);
		_triadKernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(TriadKernel);
		_resetKernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>>(ResetKernel);

		Name = $"GPU ({info})";
	}

	/// <summary>Initializes the B and C buffers used for the STREAM Triad benchmark.</summary>
	/// <param name="index">The buffer index.</param>
	/// <param name="b">The B buffer.</param>
	/// <param name="c">The C buffer.</param>
	private static void SetupKernel(Index1D index, ArrayView<float> b, ArrayView<float> c) {
		b[index] = 1;
		c[index] = 2;
	}

	/// <summary>Runs the STREAM Triad benchmark.</summary>
	/// <param name="index">The buffer index.</param>
	/// <param name="a">The A buffer.</param>
	/// <param name="b">The B buffer.</param>
	/// <param name="c">The C buffer.</param>
	private static void TriadKernel(Index1D index, ArrayView<float> a, ArrayView<float> b, ArrayView<float> c) {
		a[index] = b[index] + 3 * c[index];
	}

	/// <summary>Resets the A buffer used for the STREAM Triad benchmark.</summary>
	/// <param name="index">The buffer index.</param>
	/// <param name="a">The A buffer.</param>
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
