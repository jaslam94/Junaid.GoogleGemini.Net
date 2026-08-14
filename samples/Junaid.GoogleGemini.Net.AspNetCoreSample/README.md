# ASP.NET Core sample

A minimal-API app showing how `Junaid.GoogleGemini.Net` is meant to be used in a real service:
DI registration, text generation, **typed structured output**, **streaming**, **image generation**,
**cost governance**, the **`IChatClient`** abstraction, and **OpenTelemetry** wired to the console.

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
| `GET /spend` | Today's cumulative spend, as tracked by `ICostGovernor` |
| `POST /chat` `{ "message": "..." }` | Chat via `Microsoft.Extensions.AI.IChatClient` |

Watch the console while you hit an endpoint. You'll see the OpenTelemetry spans
(`gen_ai.system`, `gen_ai.request.model`, token counts) and metrics the library emits.

## Cost governance

`appsettings.json` configures `Gemini:Budget` with deliberately tight demo ceilings
(`MaxCostPerDayUsd: 1.00`, `MaxCostPerRequestUsd: 0.05`), so you can see it reject a call without
having to burn a real day's budget first. `GET /generate` catches both `GeminiBudgetExceededException`
(the daily ceiling) and `GeminiRequestCostExceededException` (the per-request estimate) and turns them
into a `402 Payment Required`. Hit `/generate` a few times, then `/spend` to see the running total, or
raise `MaxCostPerRequestUsd` in `appsettings.json` if `/generate` starts rejecting calls before you're
ready to see that. See [Cost governance](../../docs/articles/cost-governance.md) for the full picture.
