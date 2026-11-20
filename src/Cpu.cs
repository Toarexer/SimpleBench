using System.Diagnostics;
using System.IO.Compression;

namespace SimpleBench;

/// Represents a multicore CPU benchmark that can be initialized and ran to execute Brotli compression and decompression tasks to measure how many times they can be completed per second.
internal sealed class CpuBenchmark : Benchmark {
	/// 128 MiB
	private const int CompressionBufferSize = Gibibyte / 8;

	/// 8 MiB
	private const int RandomBufferSize = CompressionBufferSize / 16;

	/// The buffer used for populating the source buffer.
	private readonly byte[] _randomBuffer = new byte[RandomBufferSize];

	/// The buffer containing the data to be compressed during the benchmark.
	private readonly byte[] _sourceBuffer = new byte[CompressionBufferSize];

	/// <summary>The compression destination buffer.</summary>
	/// <remarks>Multiple threads are writing into this buffer simultaneously, but since the data written is always the same it is not an issue.</remarks>
	private readonly byte[] _compressedDataBuffer = new byte[CompressionBufferSize];

	/// <summary>The decompression destination buffer.</summary>
	/// <remarks>Multiple threads are writing into this buffer simultaneously, but since the data written is always the same it is not an issue.</remarks>
	private readonly byte[] _decompressedDataBuffer = new byte[CompressionBufferSize];

	/// <summary>The array containing the delegates that will be passed to <c>Parallel.Invoke</c>.</summary>
	private Action[] _actions = [];

	public override string Name => "CPU";

	public override void Init() {
		if (State != InitState.NotInitialized)
			return;
		State = InitState.Initializing;

		Random random = new(Gibibyte);
		random.NextBytes(_randomBuffer);

		int copied = 0;
		while (copied < _sourceBuffer.Length) {
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

		// This leads to race conditions when writing to _compressedDataBuffer and _decompressedDataBuffer.
		// In this case it is not an issue since all threads work with the same data.
		Parallel.Invoke(options, _actions);

		sw.Stop();
		return sw.Elapsed.TotalSeconds;
	}

	/// The benchmark task performing Brotli compression and decompression.
	private void BrotliBenchmark() {
		BrotliEncoder.TryCompress(_sourceBuffer, _compressedDataBuffer, out int compressed);
		BrotliDecoder.TryDecompress(_compressedDataBuffer.AsSpan(0, compressed), _decompressedDataBuffer, out _);
	}
}
