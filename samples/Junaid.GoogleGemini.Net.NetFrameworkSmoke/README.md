# .NET Framework runtime smoke test

A tiny `net48` console that proves the library's **netstandard2.0** build actually *runs* on the
classic .NET Framework CLR, not just that it compiles. It exercises the full path: DI registration,
options validation, `HttpClient` + the polyfilled `GeminiRetryHandler`, and System.Text.Json
source-generated (de)serialization.

It is **not** part of `Junaid.GoogleGemini.Net.sln`, because it can't build/run on the Linux CI
runners. Run it on Windows.

## Run

```powershell
$env:GeminiApiKey = "your-key"   # omit to just verify the assembly loads (no network call)
dotnet run --project samples/Junaid.GoogleGemini.Net.NetFrameworkSmoke
```

Expected with a key: `Live response: 'pong'  (finish=STOP)`.

## What it verified (and the fix it produced)

Running this surfaced a real .NET Framework-only issue that compile-only checks miss: the options
`DataAnnotations` validation needs `System.ComponentModel.Annotations` **4.2.0.0**, but the Framework
GAC ships an older version, so `AddGemini` threw `FileNotFoundException` on first use.

Fix: the library now declares `System.ComponentModel.Annotations` as a **netstandard2.0 dependency**,
so it's deployed to .NET Framework consumers automatically (this project relies on that transitive
flow, and it has no explicit reference to it).

## .NET Framework consumer checklist

- Enable binding redirects (standard for net4x SDK-style apps):
  `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>`
- That's it. The required `System.ComponentModel.Annotations` now comes in transitively with the
  package.
