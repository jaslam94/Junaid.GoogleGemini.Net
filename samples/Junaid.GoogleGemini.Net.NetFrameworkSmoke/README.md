# .NET Framework runtime smoke test

A tiny `net48` console app. It proves that the library's netstandard2.0 build runs on the classic
.NET Framework CLR. A successful compile does not prove that. It exercises the full path: DI registration,
options validation, `HttpClient` with the polyfilled `GeminiRetryHandler`, and System.Text.Json
source-generated (de)serialization.

It is not part of `Junaid.GoogleGemini.Net.sln`, because it cannot build or run on the Linux CI
runners. Run it on Windows.

## Run

```powershell
$env:GeminiApiKey = "your-key"   # omit to just verify the assembly loads (no network call)
dotnet run --project samples/Junaid.GoogleGemini.Net.NetFrameworkSmoke
```

Expected with a key: `Live response: 'pong'  (finish=STOP)`.

## What it verified, and the fix

Running this app found a real .NET Framework-only issue that compile-only checks miss. The options
`DataAnnotations` validation needs `System.ComponentModel.Annotations` 4.2.0.0, but the Framework
GAC ships an older version. As a result, `AddGemini` threw `FileNotFoundException` on first use.

The fix: the library now declares `System.ComponentModel.Annotations` as a netstandard2.0
dependency. .NET Framework consumers then receive it automatically. This project relies on that
transitive flow and has no explicit reference to the package.

## .NET Framework consumer checklist

- Enable binding redirects (standard for net4x SDK-style apps):
  `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>`
- Nothing else is needed. The required `System.ComponentModel.Annotations` now comes in
  transitively with the package.
