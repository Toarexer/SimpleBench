using System.Numerics;

namespace SimpleBench;

internal abstract class Benchmark<T> where T : INumber<T> {
	public enum InitState {
		NotInitialized,
		Initializing,
		Initialized
	}

	public const int Gibibyte = 1073741824;

	public abstract string Name { get; }

	public InitState State { get; protected set; } = InitState.NotInitialized;

	public abstract void Init();

	public abstract IEnumerable<T> Run(int times);
}
