using System.Diagnostics;
using System.IO.Compression;

namespace SimpleBench;

internal sealed class CpuBenchmark : Benchmark<double> {
	private const int RandomBufferSize = Gibibyte / 64;

	private const int CompressionBufferSize = Gibibyte;

	private readonly byte[] _randomBuffer = new byte[RandomBufferSize];

	private readonly byte[] _sourceBuffer = new byte[CompressionBufferSize];

	private readonly byte[] _compressedDataBuffer = new byte[CompressionBufferSize];

	private readonly byte[] _decompressedDataBuffer = new byte[CompressionBufferSize];

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

		State = InitState.Initialized;
	}

	public override IEnumerable<double> Run(int times) {
		if (State != InitState.Initialized)
			yield break;

		for (int i = 0; i < times; i++)
			yield return BrotliBenchmark();
	}

	private double BrotliBenchmark() {
		Stopwatch sw = new();
		sw.Start();

		BrotliEncoder.TryCompress(_sourceBuffer, _compressedDataBuffer, out int compressed);
		BrotliDecoder.TryDecompress(_compressedDataBuffer.AsSpan(0, compressed), _decompressedDataBuffer, out _);

		sw.Stop();
		return sw.Elapsed.TotalSeconds;
	}
}
