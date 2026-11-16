using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;

namespace SimpleBench;

internal static class Program {
	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true
	};

	private static int Main(string[] args) {
		Option<int> durationOption = new("--duration", "-d") {
			DefaultValueFactory = _ => 10,
			Description = "The duration of each benchmark in seconds.",
			Validators = {
				result => {
					int value = result.GetValue((Option<int>)result.Option);
					if (value < 1)
						result.AddError("The duration cannot be less than 1.");
				}
			}
		};

		Option<bool> listDevicesOption = new("--list-devices", "-l") {
			DefaultValueFactory = _ => false,
			Description = "List available devices for the graphics benchmark."
		};

		Option<bool> listPreferredDevicesOption = new("--list-preferred-devices", "-p") {
			DefaultValueFactory = _ => false,
			Description = "List preferred devices for the graphics benchmark."
		};

		Option<bool> silentOption = new("--silent", "-s") {
			DefaultValueFactory = _ => false,
			Description = "Suppress messages and only print the JSON result."
		};

		RootCommand rootCommand = new("Simple Benchmark") {
			durationOption,
			listDevicesOption,
			listPreferredDevicesOption,
			silentOption
		};

		rootCommand.SetAction(result => {
			bool listPreferred = result.GetValue(listPreferredDevicesOption);
			if (listPreferred) {
				foreach (DeviceInfo info in Utils.GetPreferredDevices())
					Console.WriteLine($"{info.Type}_{info.Index}: {info.Name}");
				return;
			}

			bool listAvailable = result.GetValue(listDevicesOption);
			if (listAvailable) {
				foreach (DeviceInfo info in Utils.GetAvailableDevices())
					Console.WriteLine($"{info.Type}_{info.Index}: {info.Name}");
				return;
			}

			int seconds = result.GetValue(durationOption);
			bool silent = result.GetValue(silentOption);
			RunBenchmarks(seconds, silent);
		});

		return rootCommand.Parse(args).Invoke();
	}

	private static void RunBenchmarks(int seconds, bool silent = false) {
		Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

		CpuBenchmark cpuBench = new();
		MemoryBenchmark memBench = new();
		using GpuBenchmarkHandler gpuBenchHandler = new();

		double cpuResults = cpuBench.RunBenchmark(seconds, silent);
		double memResults = memBench.RunBenchmark(seconds, silent);
		double[] gpuResults = [.. gpuBenchHandler.Benchmarks.Select(benchmark => benchmark.RunBenchmark(seconds, silent))];

		if (!silent)
			Console.WriteLine("---");

		string json = JsonSerializer.Serialize(
			new {
				cpuResults,
				memResults,
				gpuResults
			},
			JsonOptions
		);
		Console.WriteLine(json);
	}

	private static double RunBenchmark(this Benchmark benchmark, int seconds, bool silent = false) {
		if (!silent)
			Console.WriteLine($"Setting up {benchmark.Name} benchmark...");
		benchmark.Init();

		if (!silent)
			Console.WriteLine($"Running {benchmark.Name} benchmark for {seconds} seconds...");
		return benchmark.Run(seconds);
	}

	private static T[] RemoveFirstRunAndEdgeCases<T>(this IEnumerable<T> source) {
		return [.. source.Skip(1).Order().Skip(1).SkipLast(1)];
	}
}
