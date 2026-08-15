# Getting started

## Install

```shell
dotnet add package Junaid.GoogleGemini.Net
# optional: Microsoft.Extensions.AI adapters
dotnet add package Junaid.GoogleGemini.Net.Extensions.AI
```

Targets **net8.0**, **net9.0**, and **netstandard2.0** (for .NET Framework / Unity / Mono).

## Authenticate

Get a key from [Google AI Studio](https://aistudio.google.com/app/apikey). Provide it via the
`GeminiApiKey` environment variable or configuration:

```json
{
  "Gemini": {
    "ApiKey": "your-api-key",
    "DefaultModel": "gemini-3.7-flash",
    "TimeoutSeconds": 100,
    "MaxRetries": 3,
    "RateLimit": { "Enabled": true, "RequestsPerMinute": 60 }
  }
}
```

## Register

```csharp
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));
// or:
builder.Services.AddGemini(options => options.ApiKey = "...");
```

## Use

```csharp
public class MyService(IGeminiService gemini)
{
    public async Task<string> Ask(string prompt)
    {
        var response = await gemini.GenerateAsync(prompt);
        return response.Text();
    }
}
```

### Reading responses

`Text()` returns the text, or an **empty string** when there is none (never a placeholder). To
distinguish "no content":

```csharp
if (response.TryGetText(out var text)) { /* use text */ }
string guaranteed = response.GetTextOrThrow();   // throws GeminiContentException if blocked/empty
var finishReason = response.FinishReason;          // "STOP", "SAFETY", ...
var usage = response.Usage;                         // token counts
```

### Errors

All failures derive from `GeminiException`:

| Type | When |
|---|---|
| `GeminiApiException` | API returned a non-success status (carries `StatusCode`, parsed `Error`) |
| `GeminiRateLimitException` | the client-side rate limiter rejected the call |
| `GeminiTimeoutException` | the request timed out |
| `GeminiSerializationException` | a payload couldn't be (de)serialized |
| `GeminiContentException` | `GetTextOrThrow()` found no usable text |

Next: [Structured output](structured-output.md).
