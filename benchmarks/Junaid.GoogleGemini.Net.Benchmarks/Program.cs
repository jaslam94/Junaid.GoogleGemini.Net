using BenchmarkDotNet.Running;

// `dotnet run -c Release` with no args lists the discovered benchmark classes interactively;
// pass e.g. `--filter *Default*` (or `*` for all) to run non-interactively. There's no CI job
// running this today (it's a manual `dotnet run`, same as when the numbers published in
// docs/articles/benchmarks.md and README.md's Performance section were captured) — if one gets
// added later, update this comment to say so rather than leaving it implied.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
