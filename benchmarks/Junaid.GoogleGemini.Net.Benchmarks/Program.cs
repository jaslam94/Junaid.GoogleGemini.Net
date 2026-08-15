using BenchmarkDotNet.Running;

// `dotnet run -c Release` with no args lists the discovered benchmark classes interactively;
// pass e.g. `--filter *Default*` (or `*` for all) to run non-interactively, which is what CI/the
// numbers published in README.md use.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
