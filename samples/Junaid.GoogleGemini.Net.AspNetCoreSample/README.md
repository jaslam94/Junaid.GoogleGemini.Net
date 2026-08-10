# ASP.NET Core sample

A minimal-API app showing how `Junaid.GoogleGemini.Net` is meant to be used in a real service:
DI registration, text generation, **typed structured output**, **streaming**, the
**`IChatClient`** abstraction, and **OpenTelemetry** wired to the console.

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
| `GET /generate?prompt=...` | Basic text generation |
| `GET /recipe?dish=pancakes` | `GenerateAsync<Recipe>`, typed JSON output |
| `GET /stream?prompt=...` | Streaming chunks as they arrive |
| `POST /chat` `{ "message": "..." }` | Chat via `Microsoft.Extensions.AI.IChatClient` |

Watch the console while you hit an endpoint. You'll see the OpenTelemetry spans
(`gen_ai.system`, `gen_ai.request.model`, token counts) and metrics the library emits.
