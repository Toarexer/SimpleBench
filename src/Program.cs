using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SimpleBench;

internal static class Program {
	[Flags]
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	private enum Benchmarks {
		None   = 0b0000,
		CPU    = 0b0001,
		Memory = 0b0010,
		GPU    = 0b0100,
		All    = CPU | Memory | GPU
	}

	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true
	};

	private static int Main(string[] args) {
		Option<IEnumerable<string>> acceleratorOption = new("--accelerator", "-a") {
			Arity = ArgumentArity.OneOrMore,
			DefaultValueFactory = _ => Utils.GetPreferredDevices().Select(device => device.Id),
			Description = "The accelerators to use for the graphics benchmarks.",
		};

		Option<IEnumerable<Benchmarks>> benchmarkOption = new("--benchmark", "-b") {
			Arity = ArgumentArity.OneOrMore,
			DefaultValueFactory = _ => [Benchmarks.CPU, Benchmarks.Memory, Benchmarks.GPU],
			Description = $"The benchmark types to run. Accepts the \"{Benchmarks.All}\" value to run all benchmark types."
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
			HashSet<string> accelerators = new(result.GetValue(acceleratorOption)!, StringComparer.OrdinalIgnoreCase);
			Benchmarks benchmarks = result.GetValue(benchmarkOption)!.Aggregate(Benchmarks.None, (accumulator, next) => accumulator | next);
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
				Console.WriteLine(info);
		});

		RootCommand rootCommand = new("Simple Benchmark") {
			runBenchmarksCommand,
			listDevicesCommand
		};

		return rootCommand.Parse(args).Invoke();
	}

	private static void RunBenchmarks(HashSet<string> accelerators, Benchmarks benchmarks, int seconds, bool silent = false) {
		Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

		Dictionary<string, object> results = new();

		if (benchmarks.HasFlag(Benchmarks.CPU)) {
			results["cpu"] = new CpuBenchmark().RunBenchmark(seconds, silent);
		}

		WaitForGarbageCollection();

		if (benchmarks.HasFlag(Benchmarks.Memory)) {
			results["mem"] = new MemoryBenchmark().RunBenchmark(seconds, silent);
		}

		WaitForGarbageCollection();

		if (benchmarks.HasFlag(Benchmarks.GPU)) {
			using GpuBenchmarkHandler gpuBenchHandler = new(accelerators);
			results["gpu"] = gpuBenchHandler.Benchmarks.Select(benchmark => benchmark.RunBenchmark(seconds, silent)).ToArray();
		}

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

	private static void WaitForGarbageCollection() {
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
		GC.WaitForPendingFinalizers();
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
	}
}
