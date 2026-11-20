namespace SimpleBench;

/// Represents a benchmark that can be initialized and ran to execute tasks and measure how many times they can be completed per second.
internal abstract class Benchmark {
	/// <summary>Represents an error that occurs if the benchmark is run before it is fully initialized.</summary>
	/// <param name="component">The display name of the benchmark.</param>
	protected class NotInitializedException(string component) : Exception {
		public override string ToString() => $"{component} benchmark not initialized";
	}

	/// Represents the initialization state of benchmarks.
	public enum InitState {
		/// The benchmark initialization has not started yet.
		NotInitialized,

		/// The benchmark is currently initializing.
		Initializing,

		/// The benchmark initialization has finished.
		Initialized
	}

	/// 1 GiB
	public const int Gibibyte = 1073741824;

	/// Display name of the benchmark.
	public abstract string Name { get; }

	/// Current initialization state of the benchmark.
	public InitState State { get; protected set; } = InitState.NotInitialized;

	/// Begins initialization of the benchmark.
	public abstract void Init();

	/// <summary>Runs the benchmark task.</summary>
	/// <returns>The time it took to complete the task in seconds.</returns>
	protected abstract double RunBenchmark();

	/// <summary>Runs the benchmark task as many times as possible within the specified timeframe.</summary>
	/// <param name="seconds">The minimum duration of the benchmark in seconds. If a task is currently running it will be allowed to complete.</param>
	/// <returns>completed tasks / elapsed seconds</returns>
	/// <exception cref="NotInitializedException">The benchmark was not in the <c>Initialized</c> state.</exception>
	public virtual double Run(int seconds) {
		if (State != InitState.Initialized)
			throw new NotInitializedException(Name);

		ulong count = 0;
		double elapsed = 0;

		while (elapsed < seconds) {
			elapsed += RunBenchmark();
			count++;
		}

		return count / elapsed;
	}
}
