using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;

namespace SimpleBench;

internal sealed class GpuBenchmarkDevice : IDisposable {
	private readonly Action<Index1D, ArrayView<float>> _resetKernel;

	public Accelerator Accelerator { get; }

	public Action<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>> Kernel { get; }

	public int BufferSize { get; }

	public MemoryBuffer1D<float, Stride1D.Dense> A { get; }

	public MemoryBuffer1D<float, Stride1D.Dense> B { get; }

	public MemoryBuffer1D<float, Stride1D.Dense> C { get; }

	public GpuBenchmarkDevice(Context context, Device device, int bufferSize) {
		Accelerator = device.CreateAccelerator(context);
		Kernel = Accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(TriadKernel);

		BufferSize = bufferSize / sizeof(float);
		A = Accelerator.Allocate1D<float>(bufferSize);
		B = Accelerator.Allocate1D<float>(bufferSize);
		C = Accelerator.Allocate1D<float>(bufferSize);

		Action<Index1D, ArrayView<float>, ArrayView<float>> setupKernel = Accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(SetupKernel);
		setupKernel(BufferSize, B.View, C.View);
		Accelerator.Synchronize();

		_resetKernel = Accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>>(ResetKernel);
	}

	public void ResetBufferA() {
		_resetKernel(BufferSize, A.View);
		Accelerator.Synchronize();
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

	public void Dispose() {
		C.Dispose();
		B.Dispose();
		A.Dispose();
		Accelerator.Dispose();
	}
}

internal sealed class GpuBenchmark : Benchmark<double>, IDisposable {
	private const int TotalBufferSize = Gibibyte;

	private Context? _context;

	private GpuBenchmarkDevice[] _devices = [];

	public override string Name => "GPU";

	public override void Init() {
		if (State != InitState.NotInitialized)
			return;
		State = InitState.Initializing;

		_context = Context.CreateDefault();
		Device[] devices = [.. _context.GetPreferredDevices(false, true)];
		_devices = [.. devices.Select(device => new GpuBenchmarkDevice(_context, device, TotalBufferSize))];

		State = InitState.Initialized;
	}

	public override IEnumerable<double> Run(int times) {
		if (State != InitState.Initialized)
			yield break;

		for (int i = 0; i < times; i++)
			yield return StreamTriadBenchmark().Sum();
	}

	private IEnumerable<double> StreamTriadBenchmark() {
		foreach (GpuBenchmarkDevice device in _devices) {
			device.ResetBufferA();

			Stopwatch sw = new();
			sw.Start();

			device.Kernel(device.BufferSize, device.A.View, device.B.View, device.C.View);
			device.Accelerator.Synchronize();

			sw.Stop();
			yield return sw.Elapsed.TotalSeconds;
		}
	}

	public void Dispose() {
		foreach (GpuBenchmarkDevice device in _devices)
			device.Dispose();
		_context?.Dispose();
	}
}
