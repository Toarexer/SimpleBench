using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SimpleBench;

internal static class Program {
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	private enum Benchmarks {
		All,
		CPU,
		Memory,
		GPU
	}

	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true
	};

	private static int Main(string[] args) {
		Option<IEnumerable<string>> acceleratorOption = new("--accelerator", "-a") {
			Arity = ArgumentArity.OneOrMore,
			DefaultValueFactory = _ => [.. Utils.GetPreferredDevices().Select(device => device.Name)],
			Description = "The accelerators to use for the graphics benchmarks.",
		};

		Option<IEnumerable<Benchmarks>> benchmarkOption = new("--benchmark", "-b") {
			Arity = ArgumentArity.OneOrMore,
			DefaultValueFactory = _ => [Benchmarks.CPU, Benchmarks.Memory, Benchmarks.GPU],
			Description = "The benchmarks to run."
		};

		Option<int> durationOption = new("--duration", "-d") {
			DefaultValueFactory = _ => 10,
			Description = "The duration of each benchmark in seconds.",
			Validators = {
				result => {
					int value = result.GetValue((Option<int>)result.Option);
					if (value < 1)
						result.AddError("The duration cannot be less than 1 second.");
				}
			}
		};

		Option<bool> silentOption = new("--silent", "-s") {
			DefaultValueFactory = _ => false,
			Description = "Suppress messages and only print the JSON result."
		};

		Command runBenchmarksCommand = new("run", "Run the benchmarks.") {
			acceleratorOption,
			benchmarkOption,
			durationOption,
			silentOption
		};
		runBenchmarksCommand.SetAction(result => {
			HashSet<string> accelerators = [.. result.GetValue(acceleratorOption)!];
			HashSet<Benchmarks> benchmarks = [.. result.GetValue(benchmarkOption)!];
			int seconds = result.GetValue(durationOption);
			bool silent = result.GetValue(silentOption);

			RunBenchmarks(accelerators, benchmarks, seconds, silent);
		});

		Option<bool> listPreferredDevicesOption = new("--preferred-only", "-p") {
			DefaultValueFactory = _ => false,
			Description = "List only the preferred accelerators for the graphics benchmarks."
		};

		Command listDevicesCommand = new("list", "List the accelerators for the graphics benchmarks.") {
			listPreferredDevicesOption
		};
		listDevicesCommand.SetAction(result => {
			bool listPreferred = result.GetValue(listPreferredDevicesOption);

			foreach (DeviceInfo info in listPreferred ? Utils.GetPreferredDevices() : Utils.GetAvailableDevices())
				Console.WriteLine($"{info.Type}_{info.Index}: {info.Name}");
		});

		RootCommand rootCommand = new("Simple Benchmark") {
			runBenchmarksCommand,
			listDevicesCommand
		};

		return rootCommand.Parse(args).Invoke();
	}

	private static void RunBenchmarks(HashSet<string> accelerators, HashSet<Benchmarks> benchmarks, int seconds, bool silent = false) {
		Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

		Dictionary<string, object> results = new();

		if (benchmarks.Contains(Benchmarks.All) || benchmarks.Contains(Benchmarks.CPU)) {
			results["cpu"] = new CpuBenchmark().RunBenchmark(seconds, silent);
		}

		GC.Collect();

		if (benchmarks.Contains(Benchmarks.All) || benchmarks.Contains(Benchmarks.Memory)) {
			results["mem"] = new MemoryBenchmark().RunBenchmark(seconds, silent);
		}

		GC.Collect();

		if (benchmarks.Contains(Benchmarks.All) || benchmarks.Contains(Benchmarks.GPU)) {
			using GpuBenchmarkHandler gpuBenchHandler = new(accelerators);
			results["gpu"] = gpuBenchHandler.Benchmarks.Select(benchmark => benchmark.RunBenchmark(seconds, silent)).ToArray();
		}

		GC.Collect();

		if (!silent)
			Console.WriteLine("---");

		string json = JsonSerializer.Serialize(results, JsonOptions);
		Console.WriteLine(json);
	}

	private static double RunBenchmark(this Benchmark benchmark, int seconds, bool silent) {
		if (!silent)
			Console.WriteLine($"Setting up {benchmark.Name} benchmark...");
		benchmark.Init();

		if (!silent)
			Console.WriteLine($"Running {benchmark.Name} benchmark for {seconds} seconds...");
		return benchmark.Run(seconds);
	}
}
