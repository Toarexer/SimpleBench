namespace SimpleBench;

internal abstract class Benchmark {
	protected class NotInitializedException(string component) : Exception {
		public override string ToString() => $"{component} benchmark not initialized";
	}

	public enum InitState {
		NotInitialized,
		Initializing,
		Initialized
	}

	public const int Gibibyte = 1073741824;

	public abstract string Name { get; }

	public InitState State { get; protected set; } = InitState.NotInitialized;

	public abstract void Init();

	protected abstract double RunBenchmark();

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
