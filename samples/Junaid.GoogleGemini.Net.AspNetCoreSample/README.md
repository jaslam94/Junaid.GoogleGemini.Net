# ASP.NET Core sample

A minimal-API app that shows how to use `Junaid.GoogleGemini.Net` in a real service. It covers DI
registration, text generation, typed structured output, streaming, image generation, cost
governance, the `IChatClient` abstraction, and OpenTelemetry wired to the console.

## Run

```shell
# from the repo root
setx GeminiApiKey "your-api-key"      # Windows; or: export GeminiApiKey=your-api-key
dotnet run --project samples/Junaid.GoogleGemini.Net.AspNetCoreSample
```

(Or put the key in `appsettings.json` under `Gemini:ApiKey`, or use `dotnet user-secrets`.)

## Endpoints

| Method & route | What it shows |
|---|---|
| `GET /generate?prompt=...` | Basic text generation; also demonstrates cost governance (see below) |
| `GET /recipe?dish=pancakes` | `GenerateAsync<Recipe>`, typed JSON output |
| `GET /stream?prompt=...` | Streaming chunks as they arrive |
| `GET /image?prompt=...` | `GenerateImageAsync`, returns the generated image bytes |
| `GET /speak?text=...` | `GenerateAudioAsync`, returns a playable WAV file |
| `GET /transcribe?text=...` | Speaks the text with TTS, then transcribes the clip with `ITranscriptionService` |
| `GET /spend` | Today's cumulative spend, as tracked by `ICostGovernor` |
| `POST /chat` `{ "message": "..." }` | Chat via `Microsoft.Extensions.AI.IChatClient` |

Watch the console while you call an endpoint. It shows the OpenTelemetry spans
(`gen_ai.system`, `gen_ai.request.model`, token counts) and metrics that the library emits.

## Cost governance

`appsettings.json` sets `Gemini:Budget` with deliberately low demo ceilings
(`MaxCostPerDayUsd: 1.00`, `MaxCostPerRequestUsd: 0.05`). You can then see the library reject a call
without spending a real day's budget first. `GET /generate` catches `GeminiBudgetExceededException`
(the daily ceiling) and `GeminiRequestCostExceededException` (the per-request estimate). It turns
both into a `402 Payment Required`. Call `/generate` a few times, then call `/spend` to see the
running total. If `/generate` rejects calls sooner than you want, raise `MaxCostPerRequestUsd` in
`appsettings.json`. See [Cost governance](../../docs/articles/cost-governance.md) for details.
