# Observability (OpenTelemetry)

The library emits traces and metrics via `System.Diagnostics`, following the OpenTelemetry **GenAI
semantic conventions**. There is **no OpenTelemetry package dependency** — you opt in by subscribing
to the source and meter. If nobody listens, overhead is negligible.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(GeminiTelemetry.SourceName))
    .WithMetrics(m => m.AddMeter(GeminiTelemetry.SourceName));
```

`GeminiTelemetry.SourceName` is `"Junaid.GoogleGemini.Net"`.

## What you get

**Spans** (one per API call) tagged with:

- `gen_ai.system` = `gemini`
- `gen_ai.operation.name` (e.g. `generateContent`)
- `gen_ai.request.model`
- `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`
- `gen_ai.response.finish_reasons`
- error status on failures

**Metrics**:

- `gen_ai.client.operation.duration` (histogram, seconds)
- `gen_ai.client.token.usage` (histogram, tagged `gen_ai.token.type` = input/output)

## Exporting

Add any OpenTelemetry exporter — console, OTLP, Azure Monitor, etc. For example, the console exporter
(used by the ASP.NET Core sample):

```csharp
.WithTracing(t => t.AddSource(GeminiTelemetry.SourceName).AddConsoleExporter())
.WithMetrics(m => m.AddMeter(GeminiTelemetry.SourceName).AddConsoleExporter());
```
