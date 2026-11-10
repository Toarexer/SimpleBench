using System.Diagnostics;

namespace SimpleBench;

internal sealed class MemoryBenchmark : Benchmark<double> {
	private const int BufferSize = Gibibyte;

	private const float Scalar = 3;

	// ReSharper disable once CollectionNeverQueried.Local
	private readonly byte[] _buffer1 = new byte[BufferSize];

	private readonly byte[] _buffer2 = new byte[BufferSize];

	private readonly byte[] _buffer3 = new byte[BufferSize];

	public override string Name => "Memory";

	public override void Init() {
		if (State != InitState.NotInitialized)
			return;
		State = InitState.Initializing;

		Parallel.For(0, BufferSize, i => {
			_buffer1[i] = 0;
			_buffer1[i] = 1;
			_buffer1[i] = 2;
		});

		State = InitState.Initialized;
	}

	public override IEnumerable<double> Run(int times) {
		if (State != InitState.Initialized)
			yield break;

		for (int i = 0; i < times; i++)
			yield return StreamTriadBenchmark();
	}

	private double StreamTriadBenchmark() {
		Stopwatch sw = new();
		sw.Start();

		Parallel.For(0, BufferSize, i => {
			unchecked {
				_buffer1[i] = (byte)(_buffer2[i] + Scalar * _buffer3[i]);
			}
		});

		sw.Stop();
		return sw.Elapsed.TotalSeconds / BufferSize;
	}
}
