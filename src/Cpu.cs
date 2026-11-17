using System.Diagnostics;
using System.IO.Compression;

namespace SimpleBench;

internal sealed class CpuBenchmark : Benchmark {
	private const int CompressionBufferSize = Gibibyte / 8;

	private const int RandomBufferSize = CompressionBufferSize / 16;

	private readonly byte[] _randomBuffer = new byte[RandomBufferSize];

	private readonly byte[] _sourceBuffer = new byte[CompressionBufferSize];

	private readonly byte[] _compressedDataBuffer = new byte[CompressionBufferSize];

	private readonly byte[] _decompressedDataBuffer = new byte[CompressionBufferSize];

	private Action[] _actions = [];

	public override string Name => "CPU";

	public override void Init() {
		if (State != InitState.NotInitialized)
			return;
		State = InitState.Initializing;

		Random random = new(Gibibyte);
		random.NextBytes(_randomBuffer);

		int copied = 0;
		while (copied < _compressedDataBuffer.Length) {
			Span<byte> destination = _sourceBuffer.AsSpan(copied);
			int n = Math.Min(_randomBuffer.Length, destination.Length);
			_randomBuffer.AsSpan(0, n).CopyTo(destination);
			copied += n;
		}

		_actions = new Action[Environment.ProcessorCount - 1];
		for (int i = 0; i < _actions.Length; i++)
			_actions[i] = BrotliBenchmark;

		State = InitState.Initialized;
	}

	protected override double RunBenchmark() {
		ParallelOptions options = new() {
			MaxDegreeOfParallelism = _actions.Length
		};

		Stopwatch sw = new();
		sw.Start();

		Parallel.Invoke(options, _actions);

		sw.Stop();
		return sw.Elapsed.TotalSeconds;
	}

	private void BrotliBenchmark() {
		BrotliEncoder.TryCompress(_sourceBuffer, _compressedDataBuffer, out int compressed);
		BrotliDecoder.TryDecompress(_compressedDataBuffer.AsSpan(0, compressed), _decompressedDataBuffer, out _);
	}
}
