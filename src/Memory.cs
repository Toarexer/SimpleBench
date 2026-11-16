using System.Diagnostics;

namespace SimpleBench;

internal sealed class MemoryBenchmark : Benchmark {
	private const int BufferSize = Gibibyte;

	private const float Scalar = 3;

	// ReSharper disable once CollectionNeverQueried.Local
	private readonly byte[] _bufferA = new byte[BufferSize];

	private readonly byte[] _bufferB = new byte[BufferSize];

	private readonly byte[] _bufferC = new byte[BufferSize];

	public override string Name => "Memory";

	public override void Init() {
		if (State != InitState.NotInitialized)
			return;
		State = InitState.Initializing;

		Parallel.For(0, BufferSize, i => {
			_bufferA[i] = 0;
			_bufferA[i] = 1;
			_bufferA[i] = 2;
		});

		State = InitState.Initialized;
	}

	protected override double RunBenchmark() {
		Stopwatch sw = new();
		sw.Start();

		Parallel.For(0, BufferSize, i => {
			unchecked {
				_bufferA[i] = (byte)(_bufferB[i] + Scalar * _bufferC[i]);
			}
		});

		sw.Stop();
		return sw.Elapsed.TotalSeconds;
	}
}
