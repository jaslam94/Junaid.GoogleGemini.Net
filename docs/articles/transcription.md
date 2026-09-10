# Speech-to-text (transcription)

Gemini's dedicated transcription model, `gemini-3.5-transcribe`, turns spoken audio into text. It
adds speaker diarization, word-level timestamps, and custom vocabulary biasing on top of a plain
transcript.

This model is **not** called through `generateContent`, unlike every other feature in this library.
It lives behind a different endpoint, the Interactions API. `ITranscriptionService` wraps that for
you, so you never build the request shape by hand.

```csharp
var interaction = await transcription.TranscribeAsync(audioBytes, "audio/wav");
Console.WriteLine(interaction.GetTextOrThrow());
```

`transcription` is `ITranscriptionService`, registered by `AddGemini` alongside every other service.

> **Note:** Google's docs describe two speech-to-text surfaces: `gemini-3.5-transcribe` (this one,
> pre-recorded audio, Interactions API only) and `gemini-3.5-transcribe-live` (the Live API,
> WebSocket streaming, a different protocol not covered here). Any current chat model can also
> transcribe audio through plain `generateContent` (send audio as an inline part with a "transcribe
> this" prompt), but that path returns plain text only, no diarization or timestamps. This library
> chose the dedicated model for those two features. See `PLAN-stt.md` in the repo for the full
> reasoning and the live verification this feature was built from.

## Reading the result

`Interaction` is not a `GenerateContentResponse`. It has its own accessors:

```csharp
string text = interaction.Text();                 // "" if none, never throws
bool found = interaction.TryGetText(out var text); // false + null when there is none
string text = interaction.GetTextOrThrow();        // throws GeminiContentException if none
```

## Large files

For audio too large to send inline, upload it first through the Files API, then pass the URI:

```csharp
var file = await files.UploadFileAsync(audioBytes, "audio/wav", "meeting.wav");
var interaction = await transcription.TranscribeFileAsync(file.Uri!, "audio/wav");
```

## Diarization and word timestamps

Set `DiarizationMode` and/or `TimestampGranularities` on `TranscriptionOptions`:

```csharp
var interaction = await transcription.TranscribeAsync(audioBytes, "audio/wav", new TranscriptionOptions
{
    DiarizationMode = "speaker",
    TimestampGranularities = ["word"],
});

foreach (var word in interaction.Words())
{
    Console.WriteLine($"{word.Speaker}: {word.Text} ({word.StartOffset}-{word.EndOffset})");
}
```

Each word's `Speaker` looks like `"spk:0"`, `"spk:1"`, and so on, zero-indexed. `StartOffset`/
`EndOffset` are audio-time strings like `"0.300s"`, not numbers; parse them yourself if you need a
numeric value.

## Custom vocabulary

Set `CustomVocabulary` to bias recognition toward domain-specific terms:

```csharp
var options = new TranscriptionOptions { CustomVocabulary = ["Gemini", "Junaid.GoogleGemini.Net"] };
```

`CustomVocabulary` cannot be combined with `DiarizationMode` or `TimestampGranularities`. The API
rejects that combination with HTTP 400, so this library checks for it up front and throws
`ArgumentException` before any network call.

## Language hints

```csharp
var options = new TranscriptionOptions { LanguageCodes = ["en-US"] };
```

Optional. The model auto-detects language (85+ languages, with code-switching) when this is left
unset.

## Streaming

`StreamTranscribeAsync` yields each named event as it arrives:

```csharp
await foreach (var evt in transcription.StreamTranscribeAsync(audioBytes, "audio/wav"))
{
    if (evt.DeltaText is { } text) Console.Write(text);
    if (evt.Interaction?.Status == "completed") Console.WriteLine(evt.Interaction.Usage);
}
```

For every clip tested while building this feature (all a few seconds long), the whole transcript
arrived as a single `step.delta` event. Whether long audio streams incrementally across multiple
deltas was not confirmed; test this yourself before relying on it for a live-transcript UI.

## Polling a long-running transcription

`TranscribeAsync` returned `"completed"` synchronously in every live call made while building this
feature (a few seconds of audio each time). Google's docs allow up to an hour of audio per request;
whether that returns synchronously too was not confirmed. If you see a `Status` other than
`"completed"`, poll it:

```csharp
var interaction = await transcription.GetInteractionAsync(id);
```

## Cost and usage

Priced per 1,000,000 tokens like every other model, using `gemini-3.5-transcribe`'s entry in
`GeminiCostGovernor.DefaultPricing`. No extra setup needed: `TranscribeAsync`/`StreamTranscribeAsync`
record spend and OpenTelemetry usage automatically, the same as every other call.

> **Note:** pricing and free-tier availability came from a single fetch of
> [Google's pricing page](https://ai.google.dev/gemini-api/docs/pricing), not a live billing check.
> Re-verify before relying on it for a cost-sensitive or free-tier deployment.
