using BenchmarkDotNet.Running;

// Benchmark classes will be added in tasks 12.2–12.6
// (DispatcherBenchmarks, PipelineBenchmarks, NotificationBenchmarks, etc.)
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
