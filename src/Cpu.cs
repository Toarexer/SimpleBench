using System.Diagnostics;
using System.IO.Compression;

namespace SimpleBench;

internal sealed class CpuBenchmark : Benchmark<double> {
	private const int RandomBufferSize = Gibibyte / 64;

	private const int CompressionBufferSize = Gibibyte;

	private readonly Memory<byte> _randomBuffer = new byte[RandomBufferSize];

	private readonly Memory<byte> _sourceBuffer = new byte[CompressionBufferSize];

	private readonly Memory<byte> _compressedDataBuffer = new byte[CompressionBufferSize];

	private readonly Memory<byte> _decompressedDataBuffer = new byte[CompressionBufferSize];

	public override string Name => "CPU";

	public override void Init() {
		if (State != InitState.NotInitialized)
			return;
		State = InitState.Initializing;

		Random random = new(Gibibyte);
		random.NextBytes(_randomBuffer.Span);

		int copied = 0;
		while (copied < _compressedDataBuffer.Length) {
			Memory<byte> destination = _sourceBuffer[copied..];
			int n = Math.Min(_randomBuffer.Length, destination.Length);

			_randomBuffer[..n].CopyTo(destination);
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

		BrotliEncoder.TryCompress(_sourceBuffer.Span, _compressedDataBuffer.Span, out int compressed);
		BrotliDecoder.TryDecompress(_compressedDataBuffer[..compressed].Span, _decompressedDataBuffer.Span, out _);

		sw.Stop();
		return sw.Elapsed.TotalSeconds / _sourceBuffer.Length;
	}
}
