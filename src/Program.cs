using System.CommandLine;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;

namespace SimpleBench;

internal static class Program {
	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true
	};

	private static int Main(string[] args) {
		Option<int> countOption = new("--run-count", "-c") {
			DefaultValueFactory = _ => 5,
			Description = "The number of times to run the benchmarks.",
			Validators = {
				result => {
					int value = result.GetValue((Option<int>)result.Option);
					if (value < 1)
						result.AddError("The number of runs cannot be less than 1.");
				}
			}
		};

		Option<bool> silentOption = new("--silent", "-s") {
			DefaultValueFactory = _ => false,
			Description = "Suppress messages and only print the JSON result."
		};

		RootCommand rootCommand = new("Simple Benchmark") {
			countOption,
			silentOption
		};

		rootCommand.SetAction(result => {
			int count = result.GetValue(countOption);
			bool silent = result.GetValue(silentOption);
			RunBenchmarks(count, silent);
		});

		return rootCommand.Parse(args).Invoke();
	}

	private static void RunBenchmarks(int count, bool silent = false) {
		Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

		CpuBenchmark cpuBench = new();
		MemoryBenchmark memBench = new();

		double[] cpuResults = cpuBench.RunBenchmark(count, silent);
		double[] memResults = memBench.RunBenchmark(count, silent);

		if (!silent)
			Console.WriteLine("---");

		string json = JsonSerializer.Serialize(
			new {
				cpuResults,
				memResults
			},
			JsonOptions
		);
		Console.WriteLine(json);
	}

	private static T[] RunBenchmark<T>(this Benchmark<T> benchmark, int count, bool silent = false) where T : INumber<T> {
		if (!silent)
			Console.WriteLine($"Setting up {benchmark.Name} benchmark...");
		benchmark.Init();

		if (!silent)
			Console.WriteLine($"Running {benchmark.Name} benchmark {count} (+3) times...");
		return benchmark.Run(count + 3).RemoveFirstRunAndEdgeCases();
	}

	private static T[] RemoveFirstRunAndEdgeCases<T>(this IEnumerable<T> source) {
		return [.. source.Skip(1).Order().Skip(1).SkipLast(1)];
	}
}
