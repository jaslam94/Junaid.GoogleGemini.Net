# Plan: Speech-to-text (STT) for Junaid.GoogleGemini.Net

**Status:** Implemented and shipped in `6.6.0`. Research live-verified against the real API on
2026-09-07 (design, before any code), then live-verified again the same session after implementation,
through the actual `ITranscriptionService`/`IGeminiClient` code, not raw REST calls: inline
transcription, diarization with real distinct speaker labels, file-based transcription via the
existing Files API, streaming, and the client-side `CustomVocabulary`+`DiarizationMode` guard against
the real API's rejection.

**A deep second-pass self-review, done separately from the implementation pass, found and fixed three
real bugs before merge (§10).** None were caught by the first pass's own tests, which is exactly why
a second pass asked for by name matters, not a formality: a single build-and-test-green pass had
already happened and reported success, and still missed these. Full solution build 0 warnings on all
three targets after fixes; 172/172 unit tests (16 new total: 11 from implementation, 5 more from the
second pass's own regression tests); 39 passed/7 paid-tier-skipped/0 failed on the full live suite;
the ASP.NET Core sample's new `/transcribe` endpoint (added during the second pass, matching TTS's
`/speak` precedent) actually run and hit with a real request, round-tripping through TTS and STT
together, not just compiled.

## 1. Goal

Let a caller turn spoken audio into text using Google's dedicated `gemini-3.5-transcribe` model,
including its differentiated features: speaker diarization, word-level timestamps, custom
vocabulary biasing, and language auto-detection. This is the direct mirror of the TTS feature
shipped in `6.5.0`: TTS turns text into audio, this turns audio into text.

## 2. The architecture fork, and why this plan looks different from PLAN-tts.md

TTS turned out to fit this library's existing `generateContent` shape exactly (see `PLAN-tts.md`).
STT does not. Two paths exist, and they are genuinely different APIs, not two flavors of one call:

**Path A: plain audio understanding via `generateContent`.** Any current chat model (tested:
`gemini-3.8-flash`) already accepts audio as an `inlineData` part, the same mechanism
`GenerateWithImageAsync` already uses for images. Send a prompt like "Transcribe this audio exactly"
plus the audio bytes, and the model replies with the transcript as plain text. This library has zero
audio-input plumbing today, since `FileObject` (the type `GenerateWithImageAsync` takes) is
hard-validated as image-only at construction, but the underlying wire format is already
`generateContent`, already understood by every part of this codebase.

**Path B: the dedicated `gemini-3.5-transcribe` model via the Interactions API.** This is a newer,
different endpoint (`POST /v1beta/interactions`, not `models/{model}:generateContent`), with its own
request shape, response shape, error shape, and streaming protocol. It is the only way to get
diarization, word timestamps, and custom vocabulary; Path A returns plain text with none of that.

`PLAN-tts.md` §9 looked at the Interactions API for TTS and deliberately walked away from it. Asked
to choose here, the decision was the opposite: **build Path B**, accepting the larger lift, because
diarization and timestamps are the actual differentiators of this model and Path A cannot produce
them at all. This section exists so a future maintainer does not have to re-derive why this plan is
shaped so differently from the TTS one right after it.

## 3. What was verified live, and how

Every request/response shape below was confirmed with real REST calls against
`gemini-3.5-transcribe`, not read from docs alone. This mattered: a first web search and two
doc-summarization passes each guessed at a response shape (`output_text` at the top level, speaker
labels like `"spk_1"`, a `"mode": {"type": "verbatim", ...}` object) that turned out to be wrong or
unconfirmed in exactly the ways described below. The real shape was found by making the calls.

**Test audio.** Real speech, not a synthetic tone: generated via this library's own already-shipped
TTS (`gemini-2.5-flash-preview-tts`), decoded to a real WAV file the same way `GeneratedAudio.ToWav()`
does. Two clips: a single-speaker sentence ("The quick brown fox jumps over the lazy dog near the
river bank") and a two-speaker script (Joe/Jane, multi-speaker TTS) for diarization testing.

### 3.1 Path A confirmed (`generateContent`, for context/comparison, not this plan's target)

```json
POST https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent
{
  "contents": [{"parts": [
    {"text": "Transcribe this audio exactly."},
    {"inlineData": {"mimeType": "audio/wav", "data": "<base64 wav>"}}
  ]}]
}
```
Returned `HTTP 200`, plain-text transcript in `candidates[0].content.parts[0].text`
("The quick brown fox jumps over the lazy dog near the riverbank."), and `usageMetadata` with an
`AUDIO`-modality entry in `promptTokensDetails`, exactly like every other multimodal call this
library already parses. No new response type needed for this path; it is not being built in v1 (see
§5), but confirming it works was necessary to make the Path A vs. B decision honestly.

### 3.2 Path B confirmed (Interactions API, this plan's target)

**Confirmed real endpoint and auth:** `POST https://generativelanguage.googleapis.com/v1beta/interactions`,
same `x-goog-api-key` header this library already sends everywhere else. No separate auth setup
needed.

**Confirmed real request shape**, inline audio:
```json
{
  "model": "gemini-3.5-transcribe",
  "input": [{"type": "audio", "data": "<base64 wav>", "mime_type": "audio/wav"}],
  "generation_config": {
    "transcription_config": {
      "language_codes": ["en-US"],
      "timestamp_granularities": ["word"],
      "diarization_mode": "speaker"
    }
  }
}
```
All field names are `snake_case`, unlike `generateContent`'s `camelCase`. This library already
requires an explicit `[JsonPropertyName]` on every property regardless of endpoint (there is no
global naming policy to lean on), so this is a data point to get right per-field, not a new
mechanism to build.

**Confirmed real request shape**, file reference instead of inline bytes: same body, with
`{"type": "audio", "uri": "https://generativelanguage.googleapis.com/v1beta/files/...", "mime_type": "audio/wav"}`
in place of `data`. The `uri` is exactly what this library's existing `IFileService.UploadFileAsync`
already returns as `FileResource.Uri`. **This means the Files API integration point is free**: no new
upload code needed, `ITranscriptionService` just needs to accept a `FileResource`/URI the same way
`GenerateWithImageAsync` accepts a `FileObject`. Confirmed live: uploaded a WAV via the existing
resumable-upload flow, then transcribed it by URI, `HTTP 200`, correct transcript.

**Confirmed real response shape**, non-streaming:
```json
{
  "id": "v1_Chc...",
  "status": "completed",
  "usage": { "...": "see §3.3, this is not what it looks like" },
  "created": "2026-09-07T14:36:03Z",
  "updated": "2026-09-07T14:36:03Z",
  "service_tier": "standard",
  "steps": [{
    "type": "model_output",
    "content": [{
      "type": "text",
      "text": "The quick brown fox jumps over the lazy dog near the riverbank.",
      "annotations": [
        {"type": "word_info", "text": "The", "start_index": 0, "end_index": 3,
         "start_offset": "0.300s", "end_offset": "0.400s"}
      ]
    }]
  }],
  "object": "interaction",
  "model": "gemini-3.5-transcribe"
}
```
No top-level `output_text` field, contrary to what both a web search summary and a doc-fetch summary
independently guessed. The transcript lives at `steps[].content[].text`, same place regardless of
whether diarization/timestamps were requested; `annotations` is present only when
`timestamp_granularities` was set.

**Confirmed real diarization shape**, live-tested on the two-speaker Joe/Jane clip with
`diarization_mode: "speaker"` and `timestamp_granularities: ["word"]` together: each word's
`annotations` entry gets a `"speaker": "spk:0"` / `"spk:1"` field (zero-indexed, colon-separated).
This is a different literal format than the docs guessed (`"spk_1"`, underscore, one-indexed);
getting this exactly right matters for any diarized-transcript-grouping helper built on top of it.

**Confirmed: `GET /v1beta/interactions/{id}` retrieves a completed interaction by ID**, live-tested
immediately after a POST. Useful for polling. One real difference from the POST response: the GET
response's `steps` array includes an extra leading `"type": "user_input"` step echoing what was sent
(`{"uri": "...", "mime_type": "audio/wav", "type": "audio"}`), which the POST response does not
include. Any `Interaction`-parsing code must treat `steps[].type` as a real discriminator, not assume
every step is `model_output`.

**Confirmed real streaming shape**, `POST .../interactions?alt=sse` with `"stream": true` in the
body. This is a materially different SSE protocol than `streamGenerateContent?alt=sse`, which this
library already parses as one `data: {GenerateContentResponse}` event per chunk. Interactions
streaming sends a sequence of **named, differently-shaped events**:
```
event: interaction.created      data: {"interaction": {...status: "in_progress"...}}
event: interaction.status_update data: {"interaction_id": "...", "status": "in_progress"}
event: step.start               data: {"index": 0, "step": {"type": "model_output"}}
event: step.delta               data: {"index": 0, "delta": {"text": "...", "type": "text"}}
event: step.stop                data: {"index": 0}
event: interaction.completed    data: {"interaction": {...full object, status: "completed"...}}
event: done                     data: [DONE]
```
For the short test clip, the whole transcript arrived in a single `step.delta`; a longer clip would
presumably arrive as multiple deltas per step, but this was not tested with long audio (see §7, open
items). A working streaming client needs a real per-event-type parser, not a reuse of the existing
single-shape SSE loop.

### 3.3 Confirmed real usage/cost quirk (read this before wiring cost governance)

```json
"usage": {
  "total_tokens": 147,
  "total_input_tokens": 147,
  "input_tokens_by_modality": [{"modality": "audio", "tokens": 146}, {"modality": "text", "tokens": 1}],
  "total_cached_tokens": 0,
  "total_output_tokens": 0,
  "total_tool_use_tokens": 0,
  "total_thought_tokens": 0,
  "raw_prompt_token": 553,
  "model_invocation_token_counts": [{
    "prompt_tokens_details": [{"modality": "text", "tokens": 407}, {"modality": "audio", "tokens": 146}],
    "candidates_tokens_details": [{"modality": "text", "tokens": 14}]
  }]
}
```
**`total_output_tokens` reads `0` in every single live call made in this session, including ones
that clearly produced real output text.** The real output token count (14, in this example) is
buried in `model_invocation_token_counts[].candidates_tokens_details[].tokens` instead. Also notice
`total_tokens` (147) equals `total_input_tokens` (147) exactly: the top-level total does not include
output tokens either. This is the kind of thing that looks like a copy-paste field-name bug in
Google's own usage accounting for this endpoint, confirmed reproducible across nine separate live
calls in this session (every probe in §3.2's testing), not a one-off glitch. **If a future
`RecordSpend`-equivalent for this feature reads `total_output_tokens` directly, it will silently
record $0 output cost forever.** Any usage adapter must read output tokens from
`model_invocation_token_counts`, not the top-level `usage` fields.

This is also why `GeminiCostGovernor.RecordSpend(string?, UsageMetadata)` cannot be reused as-is:
its parameter type is `generateContent`'s `UsageMetadata` shape (`PromptTokenCount`,
`CandidatesTokenCount`, ...), and Interactions' `usage` object has different field names and the
quirk above. A mapping function, not a shared type, is needed. See §4.5.

### 3.4 Confirmed error shape, and why it breaks the existing error path if reused unmodified

```json
{"error": {"message": "custom_vocabulary is incompatible with diarization.", "code": "invalid_request"}}
```
This library's existing `ApiErrorResponse`/`ApiError` (`Models/GoogleApi/ApiErrorResponse.cs`) types
`Code` as `int` (matching `generateContent`'s `{"error": {"code": 404, "message": "...", "status": "NOT_FOUND"}}`).
Interactions' `code` is a **string** enum (`"invalid_request"`, `"not_found"`, confirmed via a bad
model name and a bad base64 payload). Deserializing an Interactions error body with the existing
`ApiErrorResponse` type throws a `JsonException` on the int/string mismatch; `GeminiClient.HandleResponse`
catches that specifically and rethrows as `GeminiSerializationException("Failed to parse API response", ...)`,
discarding the real, helpful message the API actually sent back. **Reusing the existing `PostAsync`/
`HandleResponse` pipeline unmodified for Interactions calls would turn every clean 400 into an opaque
serialization failure.** A new error type and a new client method are needed; see §4.2.

Confirmed error messages are genuinely useful and worth surfacing directly to callers:
- Bad model name: `"Model 'gemini-3.5-transcribe-nonexistent' not found. Did you mean 'gemini-3.5-transcribe'? ..."`
- Corrupt base64: `"The value is invalid for 'input[0].data'. Expected string, corrupt base64"`
- Missing model: `"Provide a 'agent', or 'model' parameter."`
- Confirmed live: `custom_vocabulary` really is rejected (`HTTP 400`) when combined with
  `diarization_mode` or (by the same message) `timestamp_granularities`, matching Google's docs.
  Worth validating client-side before the call, not just letting the API 400.

### 3.5 Confirmed field acceptance (schema-only; behavioral effect not fully verified)

Each of these was sent in `generation_config.transcription_config` and returned `HTTP 200` (vs. an
`"Unknown parameter"` `400` for a made-up field name, which is how the two rejected guesses below
were caught):
- `language_codes: ["en-US"]`: accepted.
- `custom_vocabulary: ["Gemini"]`: accepted alone, rejected when combined with diarization (§3.4).
- `diarization_mode: "speaker"`: accepted, and produces real per-word `speaker` labels (§3.2).
- `timestamp_granularities: ["word"]`: accepted, produces real `start_offset`/`end_offset` (§3.2).
- `timestamp_granularities: ["segment"]` and `["word", "segment"]`: both accepted without error, but
  segment-level output was not confirmed to actually differ, since the test clip was one short sentence.
- `mode: "smart"` and `mode: {"type": "verbatim"}`: both accepted without error, but neither was
  confirmed to change output for a clean, short test clip. Re-verify behavior before documenting this
  field as load-bearing, or drop it from v1 and let a caller pass it through opaquely if they want it.

**Rejected as fields** (confirms they are not real top-level `transcription_config` keys):
`diarization_config` (nested object), `enable_diarization`, `bogus_field_xyz`. The Interactions API
does validate unknown parameters and say so clearly, which made this kind of probing reliable.

## 4. Explicit scope

**In scope (v1):**
- `TranscribeAsync`: single-call transcription of inline audio bytes or a Files-API URI, against
  `gemini-3.5-transcribe`, via the Interactions API.
- Diarization (`DiarizationMode`), word timestamps (`TimestampGranularities`), custom vocabulary
  (`CustomVocabulary`), and language hints (`LanguageCodes`) as request options, all live-verified
  in §3.2/§3.5.
- Client-side validation that `CustomVocabulary` is not combined with `DiarizationMode`/
  `TimestampGranularities`, throwing a clear `ArgumentException` before the call, instead of letting
  the API 400 (confirmed real API behavior, §3.4).
- A new `Interaction` response type with `Text()`/`TryGetText()` (concatenates `model_output` step
  text, mirrors `GenerateContentResponse.Text()`) and a `Words()` helper flattening `word_info`
  annotations, including `Speaker` when diarization was requested.
- `StreamTranscribeAsync`: real incremental streaming, parsing the named-event SSE protocol from
  §3.2, not a fake wrapper around the non-streaming call.
- `GetInteractionAsync(id)`: wraps the confirmed `GET /v1beta/interactions/{id}`, for polling.
- Cost governance: a `gemini-3.5-transcribe` pricing entry and a usage-mapping function that reads
  the real output-token location from §3.3, not the misleading top-level field.
- Reuses the existing `IFileService` unchanged for the file-URI input path (§3.2); no new upload code.

**Explicitly out of scope for this change (do not implement):**
- **Path A** (plain-text transcription via `generateContent` on any chat model). It works (§3.1) and
  needs no Interactions API, but it was evaluated and passed over for v1 in favor of the dedicated
  model's diarization/timestamps, which is the actual point of this feature. Revisit only if v1's
  Interactions build turns out to be a bad bet (see §2's reasoning); do not build both paths at once.
- `gemini-3.5-transcribe-live` (the Live API / WebSocket streaming variant). Separate protocol
  entirely (WebSocket, not SSE-over-HTTP), and this library's existing "Live API's bidirectional
  audio" backlog item in `ROADMAP.md` already covers that ground. Do not fold it into this feature.
- Batch transcription of many files in one call. The Interactions API has no batch endpoint as of
  this writing (confirmed via two independent doc sources, not independently live-tested since there
  is nothing to call); a caller needing to transcribe many files loops `TranscribeAsync` for now.
- Audio longer than a few seconds was not tested. Google's docs claim up to 1 hour per request (30
  minutes when diarization/timestamps are enabled), and that the call may not return synchronously
  for long audio (untested: does a long request block until done, or return `"status": "in_progress"`
  requiring a `GetInteractionAsync` poll loop?). Confirm this before shipping; every live call in this
  plan returned `"status": "completed"` immediately because every test clip was a few seconds long.
- 85+ language codes and the diarization speaker cap (8, per docs) as C# constants. Same reasoning as
  TTS's voice names (`PLAN-tts.md` §3): plain strings, not an enum, so new codes/limits Google adds
  don't require a library update.
- `mode`/`"smart"` vs `"verbatim"` as a strongly-typed option. Accepted by the API (§3.5) but its
  actual behavioral effect was not confirmed; expose as a passthrough `string?` if included at all,
  don't build named constants around unverified behavior.

## 5. Architecture

### 5.1 New model types (`Models/GoogleApi/`, flat, matching the existing Batch/File convention)

```csharp
// InteractionRequest.cs
public class InteractionRequest
{
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("input")] public List<InteractionInput>? Input { get; set; }
    [JsonPropertyName("generation_config")] public InteractionGenerationConfig? GenerationConfig { get; set; }
    [JsonPropertyName("stream")] public bool? Stream { get; set; }
}

public class InteractionInput
{
    [JsonPropertyName("type")] public string? Type { get; set; }  // "audio"
    [JsonPropertyName("data")] public string? Data { get; set; }  // base64, mutually exclusive with Uri
    [JsonPropertyName("uri")] public string? Uri { get; set; }    // Files API URI
    [JsonPropertyName("mime_type")] public string? MimeType { get; set; }
}

public class InteractionGenerationConfig
{
    [JsonPropertyName("transcription_config")] public TranscriptionConfig? TranscriptionConfig { get; set; }
}

public class TranscriptionConfig
{
    [JsonPropertyName("language_codes")] public List<string>? LanguageCodes { get; set; }
    [JsonPropertyName("custom_vocabulary")] public List<string>? CustomVocabulary { get; set; }
    [JsonPropertyName("diarization_mode")] public string? DiarizationMode { get; set; }
    [JsonPropertyName("timestamp_granularities")] public List<string>? TimestampGranularities { get; set; }
}
```

```csharp
// Interaction.cs (response)
public class Interaction
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("usage")] public InteractionUsage? Usage { get; set; }
    [JsonPropertyName("created")] public DateTimeOffset? Created { get; set; }
    [JsonPropertyName("updated")] public DateTimeOffset? Updated { get; set; }
    [JsonPropertyName("service_tier")] public string? ServiceTier { get; set; }
    [JsonPropertyName("steps")] public List<InteractionStep>? Steps { get; set; }
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }

    // Text()/TryGetText()/Words(): concatenate/flatten across Steps where Type == "model_output",
    // mirroring GenerateContentResponse.Text()'s null-safety and GetXOrThrow() naming.
}

public class InteractionStep
{
    [JsonPropertyName("type")] public string? Type { get; set; }  // "model_output" | "user_input"
    [JsonPropertyName("content")] public List<InteractionContent>? Content { get; set; }
}

public class InteractionContent
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("annotations")] public List<InteractionAnnotation>? Annotations { get; set; }
}

public class InteractionAnnotation
{
    [JsonPropertyName("type")] public string? Type { get; set; }  // "word_info"
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("start_index")] public int? StartIndex { get; set; }
    [JsonPropertyName("end_index")] public int? EndIndex { get; set; }
    [JsonPropertyName("start_offset")] public string? StartOffset { get; set; }  // e.g. "0.300s"
    [JsonPropertyName("end_offset")] public string? EndOffset { get; set; }
    /// <summary>Zero-indexed, e.g. "spk:0". Only present when diarization was requested.</summary>
    [JsonPropertyName("speaker")] public string? Speaker { get; set; }
}
```

```csharp
// InteractionUsage.cs
public class InteractionUsage
{
    [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    [JsonPropertyName("total_input_tokens")] public int TotalInputTokens { get; set; }
    [JsonPropertyName("input_tokens_by_modality")] public List<ModalityTokenCount>? InputTokensByModality { get; set; }
    [JsonPropertyName("total_cached_tokens")] public int TotalCachedTokens { get; set; }
    /// <summary>
    /// CONFIRMED LIVE (see PLAN-stt.md §3.3): reads 0 even when real output text was produced, in
    /// every one of nine live calls tested. Do not use this for cost governance; read
    /// <see cref="ModelInvocationTokenCounts"/> instead.
    /// </summary>
    [JsonPropertyName("total_output_tokens")] public int TotalOutputTokens { get; set; }
    [JsonPropertyName("total_thought_tokens")] public int TotalThoughtTokens { get; set; }
    [JsonPropertyName("model_invocation_token_counts")] public List<ModelInvocationTokenCount>? ModelInvocationTokenCounts { get; set; }
}

public class ModalityTokenCount
{
    [JsonPropertyName("modality")] public string? Modality { get; set; }
    [JsonPropertyName("tokens")] public int Tokens { get; set; }
}

public class ModelInvocationTokenCount
{
    [JsonPropertyName("prompt_tokens_details")] public List<ModalityTokenCount>? PromptTokensDetails { get; set; }
    /// <summary>The real output token count; see the warning on <see cref="InteractionUsage.TotalOutputTokens"/>.</summary>
    [JsonPropertyName("candidates_tokens_details")] public List<ModalityTokenCount>? CandidatesTokensDetails { get; set; }
}
```

```csharp
// InteractionsApiErrorResponse.cs
public class InteractionsApiErrorResponse
{
    [JsonPropertyName("error")] public InteractionsApiError? Error { get; set; }
}

public class InteractionsApiError
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    /// <summary>e.g. "invalid_request", "not_found". A string, unlike generateContent's numeric code.</summary>
    [JsonPropertyName("code")] public string? Code { get; set; }
}
```

All new root types need `[JsonSerializable(typeof(...))]` entries in `GeminiJsonContext.cs`, same as
every other root type (see the existing list at `GeminiJsonContext.cs:25-48`); nested types under a
registered root are auto-discovered, same as `SpeechConfig`'s children were for TTS.

### 5.2 `GeminiApiException` gets a new optional field, not a new exception type

Rather than inventing a parallel exception type, add one nullable property:
```csharp
/// <summary>Machine-readable error code from the Interactions API (e.g. "invalid_request"), if this
/// exception came from that API. Null for generateContent-sourced exceptions; use <see cref="Status"/>
/// for those instead.</summary>
public string? InteractionErrorCode { get; }
```
Keep `ApiError`/`Status`/`ErrorCode` meaning exactly what they already mean for `generateContent`;
don't force-fit Interactions' string `code` into `ApiError.Code` (which is `int`) or overload
`ApiError.Status`'s existing meaning. `StatusCode` (the HTTP status) already covers what the
Interactions error body doesn't separately provide.

### 5.3 New `GeminiClient` methods, not a reuse of `PostAsync`/`StreamAsync`

Two new methods on `IGeminiClient`/`GeminiClient`, parallel to the existing `PostAsync`/`StreamAsync`
but adapted for the three confirmed differences (§3.2-§3.4): error body shape, response type, and
telemetry model-tagging (`GeminiTelemetry.Parse` extracts the model from a `models/{model}:...` URL
segment that Interactions requests don't have; the model lives in the JSON body instead, so it must
be passed through explicitly rather than parsed from the endpoint string).

```csharp
Task<Interaction> PostInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default);
IAsyncEnumerable<InteractionStreamEvent> StreamInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default);
```

`StreamInteractionAsync` parses the real named-event protocol from §3.2 (`interaction.created`,
`step.delta`, `interaction.completed`, etc.), not a reuse of the single-shape SSE loop
`StreamCoreAsync` already has. A new small `InteractionStreamEvent` type (event name + whichever
payload it carries) is the cleanest yield type; accumulating that into a running transcript is a
concern for `TranscriptionService`, not the client.

### 5.4 New `ITranscriptionService`, sibling to `IFileService`/`IBatchService`, not folded into `IGeminiService`

`IGeminiService`'s own doc comment calls it "the unified service interface for all Gemini content
generation operations," and every method on it returns or streams `GenerateContentResponse`. Adding
a method that returns `Interaction` instead would break that contract for no benefit. This codebase
already has precedent for a focused sibling interface per distinct concern (`IFileService`,
`IBatchService`, `ICachingService`), each registered the same way in `GeminiExtensions.cs`
(`services.AddTransient<IXService, XService>()`). `ITranscriptionService` follows that pattern.

```csharp
public interface ITranscriptionService
{
    /// <summary>Transcribes inline audio bytes. For large files, prefer <see cref="TranscribeFileAsync"/>
    /// via <see cref="Services.Interfaces.IFileService"/> instead of inlining base64 (inline size limit
    /// unconfirmed for this endpoint specifically; do not assume generateContent's ~20MB applies).</summary>
    Task<Interaction> TranscribeAsync(
        byte[] audioBytes, string mimeType,
        TranscriptionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Transcribes audio already uploaded via <see cref="Services.Interfaces.IFileService.UploadFileAsync"/>.</summary>
    Task<Interaction> TranscribeFileAsync(
        string fileUri, string mimeType,
        TranscriptionOptions? options = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Interaction> StreamTranscribeAsync(
        byte[] audioBytes, string mimeType,
        TranscriptionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Polls a long-running transcription by ID (confirmed live, §3.2). Needed if long audio
    /// turns out not to complete synchronously; see the open item in §7.</summary>
    Task<Interaction> GetInteractionAsync(string id, CancellationToken cancellationToken = default);
}

public class TranscriptionOptions
{
    public string Model { get; set; } = GeminiConstants.Models.Gemini35Transcribe;
    public IReadOnlyList<string>? LanguageCodes { get; set; }
    public IReadOnlyList<string>? CustomVocabulary { get; set; }
    public string? DiarizationMode { get; set; }
    public IReadOnlyList<string>? TimestampGranularities { get; set; }

    // Constructing a request throws ArgumentException up front if CustomVocabulary is set together
    // with DiarizationMode or TimestampGranularities: confirmed real 400 otherwise (§3.4).
}
```

### 5.5 New constants (`GeminiConstants`)

```csharp
// In Models, a new region alongside the TTS one, not mixed into ContentGenerationModels
// (this model is not generateContent-based, see §2).
public const string Gemini35Transcribe = "gemini-3.5-transcribe";
public static string RecommendedTranscription => Gemini35Transcribe;
```

### 5.6 Cost governance

New `gemini-3.5-transcribe` pricing entry: $2.00 in / $12.00 out per 1M tokens, free tier available
(from a single fetch of the pricing page, not a live billing check; re-verify before merging, same
standing caveat as every other pricing entry). This needs a small usage-mapping function, not a
`UsageMetadata` reuse: `InteractionUsage` has different field names than `generateContent`'s
`UsageMetadata`, and per §3.3, output tokens must be summed from
`ModelInvocationTokenCounts[].CandidatesTokensDetails[].Tokens`, not read from
`InteractionUsage.TotalOutputTokens` directly. Whether this maps into a `UsageMetadata` instance
(reusing `RecordSpend`'s existing signature) or gets a small parallel `RecordSpend`-equivalent is an
implementation-time call; either way, the mapping function and its rationale belong in code comments
next to wherever it lives, given how non-obvious the quirk is.

### 5.7 Telemetry

Cannot reuse `GeminiTelemetry.Parse(endpoint)` unmodified: it extracts the model from a
`models/{model}:operation` URL segment, and Interactions requests have neither (`endpoint` is just
`"interactions"`, model is in the request body). `PostInteractionAsync`/`StreamInteractionAsync` need
to pass the model through explicitly to `GeminiTelemetry.StartOperation`/`RecordUsage`/`RecordDuration`
rather than relying on URL parsing.

## 6. Naming and design decisions worth recording

- **`ITranscriptionService`, not a method on `IGeminiService`.** See §5.4. `Interaction` is not a
  `GenerateContentResponse`; folding it in would break that interface's own stated contract.
- **New `PostInteractionAsync`/`StreamInteractionAsync`, not a reuse of `PostAsync`/`StreamAsync`.**
  See §3.4, §3.2, §5.3: the error shape, response type, and telemetry model-tagging all differ in
  ways that would silently misbehave (an opaque `GeminiSerializationException` instead of the API's
  real message; no model tag on telemetry spans) if the existing generic methods were reused as-is.
- **A new field on `GeminiApiException`, not a new exception type.** See §5.2. Keeps one exception
  type for both APIs (simpler for callers doing a single `catch`), without corrupting what `ApiError`
  already means for `generateContent`.
- **No enum for `DiarizationMode`/`TimestampGranularities`/language codes/custom vocabulary.** Same
  reasoning this library already applies to TTS voice names (`PLAN-tts.md` §3) and model names
  generally: plain strings, so new values Google adds don't require a library update.
- **Path A (`generateContent` audio understanding) deliberately not built alongside Path B.** See §2
  and §4. Confirmed working, but building both at once means shipping two ways to do the same thing
  with different capability levels; Path B is a strict superset of what Path A can produce for this
  use case, so there is no reason to ship the weaker path unless Path B's larger lift turns out to be
  a mistake.

## 7. Open items to verify before writing code (do not skip)

- [x] **Diarization producing real, distinct speaker labels through the typed client**, not just
      accepted by the raw API. Resolved: `TranscribeAsync_WithDiarizationAndTimestamps_ReturnsDistinctSpeakerLabels`
      (live test) confirms at least 2 distinct `"spk:N"` labels and real `StartOffset`/`EndOffset`
      values on a real two-speaker clip, through `ITranscriptionService`/`Interaction.Words()`, not
      raw REST.
- [x] **The `CustomVocabulary`+`DiarizationMode` client-side guard actually matches the real API's
      rejection**, not just assumed from the earlier raw-REST probe. Resolved:
      `PostInteractionAsync_CustomVocabularyWithDiarization_RealApiRejectsWithInvalidRequest` (live
      test) bypasses `TranscriptionOptions.Validate()` and calls `IGeminiClient.PostInteractionAsync`
      directly with the conflicting combination; confirms a real `HTTP 400`/`"invalid_request"`.
- [ ] **Long audio, synchronous or not.** Still open. Every live call made in this session (research
      and post-implementation both) used a clip a few seconds long and got `"status": "completed"`
      immediately. Google's docs claim up to 1 hour of audio per request. `GetInteractionAsync` exists
      in `ITranscriptionService` for polling if long audio needs it, but this was never exercised
      against real long audio, so whether it is actually needed remains unconfirmed.
- [ ] **Inline `data` size limit.** Still open. Not tested with a large payload. Do not assume
      `generateContent`'s informal ~20MB inline guidance applies to Interactions;
      `ITranscriptionService.TranscribeFileAsync` (the Files API path) exists as the documented
      escape hatch, but no threshold for when to prefer it was established.
- **`mode` field.** Resolved by scoping out: not implemented in v1 (`TranscriptionConfig` has no
  `Mode` property), per §4's explicit scope decision. Revisit only with a real confirmed behavior to
  build a typed option around.
- [ ] **`timestamp_granularities: ["segment"]`'s actual output shape.** Still open. `TranscriptionConfig.TimestampGranularities`
      accepts any string list (not enum-locked, see §6), so a caller can already pass `"segment"`
      today; this library's own tests only exercised `"word"`.
- [ ] **Custom vocabulary's actual bias effect.** Still open. Confirmed live as an accepted field and
      confirmed incompatible with diarization (§3.4), but never tested for whether it actually changes
      recognition of an unusual term.
- [ ] **Streaming with a long clip.** Still open. Every streaming test made in this session (research
      and post-implementation) returned the entire transcript as a single `step.delta`. Whether a long
      clip arrives as multiple deltas remains unconfirmed.

## 8. Tests to add (all implemented; §7's remaining open items were left as such, not force-tested)

**Unit** (`tests/Junaid.GoogleGemini.Net.Tests/TranscriptionTests.cs`), `FakeHttpMessageHandler`-based, 11 tests:
- [x] `TranscribeAsync` sends the correct `InteractionRequest` shape for inline audio, and for a file URI.
- [x] `TranscriptionOptions` throws `ArgumentException` when `CustomVocabulary` is combined with
      `DiarizationMode` or `TimestampGranularities`, without making a network call.
- [x] A fake Interactions error body (`{"error": {"code": "invalid_request", "message": "..."}}`)
      deserializes correctly and surfaces via `GeminiApiException.InteractionErrorCode`/`.Message`,
      not a `GeminiSerializationException`.
- [x] The usage-mapping function reads output tokens from `ModelInvocationTokenCounts`, and a fake
      response with `TotalOutputTokens: 0` but real `CandidatesTokensDetails` still prices output cost
      correctly (this is the regression test for §3.3's quirk).
- [x] `Interaction.Text()`/`Words()` correctly skip a `"user_input"` step and only aggregate
      `"model_output"` steps.

**Live** (`tests/Junaid.GoogleGemini.Net.IntegrationTests/TranscriptionLiveTests.cs`), `[RequiresGeminiKey]`, 5 tests:
- [x] `TranscribeAsync` against real inline audio (generated via this library's own TTS, same pattern
      used in this plan's research) returns the expected transcript.
- [x] `TranscribeAsync` with `DiarizationMode` + `TimestampGranularities` against a real two-speaker clip
      returns per-word `Speaker` labels that actually distinguish the two speakers.
- [x] `PostInteractionAsync` with `CustomVocabulary` + `DiarizationMode` together, called directly
      (bypassing the client-side guard) against the real API, confirms a real `HTTP 400`/
      `"invalid_request"` matching what the guard prevents.
- [x] `TranscribeFileAsync` against a file uploaded through the existing `IFileService` succeeds.
- [x] `StreamTranscribeAsync` yields events and a final completed `Interaction` with `Usage`.
- [ ] Long-audio behavior (§7): not tested. No long audio sample was available in this session; left
      as an explicit open item rather than skipped silently.

Full suite results at the time of shipping: 167/167 unit tests (11 new), full solution build 0
warnings on all three targets, 39 passed/7 paid-tier-skipped/0 failed on the complete live suite (5
new STT tests plus every pre-existing live test, confirming no regression).

## 9. Docs to update

- [x] New `docs/articles/transcription.md`, following `docs/articles/tts.md`'s structure.
- [x] Added to `docs/articles/toc.yml` and `docs/index.md`'s guide list.
- [x] `README.md`: new "Speech-to-text" bullet plus a short code sample, matching the TTS section's format.
- [x] `Junaid.GoogleGemini.Net.csproj`'s `PackageReleaseNotes`: `6.6.0` entry added.
- [x] `ROADMAP.md`: added a `6.6.0` shipped-feature entry; there was no explicit STT-shaped backlog
      item to remove, confirmed by checking (see this section's original note). Also annotated the
      existing TTS-era Interactions API backlog item to point at this plan's §2 for why STT's answer
      to "should this use the Interactions API" went the other way from TTS's.
- [x] Written in this repo's `CLAUDE.md` style throughout: short sentences, no em dashes (checked with
      a literal grep across every new/changed file before publishing, not just a visual scan).

## 10. Deep second-pass review: three real bugs found and fixed before merge

Asked explicitly for a second, separate review pass after the implementation was already built,
tested (167/167 unit, 39/39 non-skipped live), and PR'd. Re-reading every changed file fresh, as if
seeing it for the first time, found three real bugs none of the first pass's own tests caught. None
of these were caught by "build succeeds, tests pass": all three are the kind of thing a test suite
only catches if someone thinks to write the specific test for it, which is exactly what a second pass
is for.

**Bug 1: `StreamInteractionAsync` mutated the caller's own `InteractionRequest` object.**
`request.Stream = true;` was set directly on the object the caller passed in, not a copy. This
library's own established convention (`GenerateAudioAsync`/`StreamAudioAsync`'s doc comment: "on a
cloned options object") exists precisely to avoid this: a caller who built one `InteractionRequest`
and reused it for both a `PostInteractionAsync` call and a `StreamInteractionAsync` call would have
silently gotten `"stream": true` on the non-streaming call too, since the same object got mutated in
place. Fixed to clone. Regression test:
`GeminiClientTests.StreamInteractionAsync_DoesNotMutateCallersRequestObject`.

**Bug 2: `StreamTranscribeAsync` was not an async iterator, so validation threw synchronously at call
time instead of on first `MoveNextAsync()`.** Every other `IAsyncEnumerable`-returning method in this
codebase (`StreamAsync`, `StreamWithImageAsync`, `StreamAudioAsync`) is an `async IAsyncEnumerable`
iterator (`yield return`), which defers everything, including argument validation, to first
enumeration. `StreamTranscribeAsync` instead ran its validation immediately and returned the
downstream enumerable directly. A caller who wraps only the `await foreach` in a try/catch (the
natural pattern given how every other streaming method here behaves) would have had a validation
`ArgumentException` escape uncaught. Fixed to a real iterator method. Regression test:
`TranscriptionTests.StreamTranscribeAsync_DefersValidationToFirstMoveNext_NotAtCallTime`.

**Bug 3 (minor, no observable effect, fixed anyway): `MapUsage`'s `TotalTokenCount` excluded thought
tokens.** `generateContent`'s real `totalTokenCount` = prompt + candidates + thoughts (confirmed from
a live call earlier this session: 8 + 2 + 96 = 106). The mapper computed only prompt + candidates.
Harmless today, since nothing in `ComputeCost`/`RecordSpend`/telemetry actually reads
`TotalTokenCount`, only the individual fields, but wrong if that ever changes or if a caller ever
inspects the mapped value directly. Fixed. Also added an explicit code comment flagging that
`CachedContentTokenCount`'s subset-of-input assumption (borrowed from `generateContent`'s semantics)
was never actually exercised live, since every observed `TotalCachedTokens` was `0` (transcription
requests have no way to attach cached content today). Regression test:
`GeminiClientTests.PostInteractionAsync_MapsUsage_ReadingOutputTokensFromModelInvocationTokenCounts_NotTheMisleadingTopLevelField`
asserts the corrected value directly.

**Also found during the same pass, not bugs but real gaps, both fixed:**
- Zero unit test coverage existed for `StreamTranscribeAsync`/`StreamInteractionAsync` before this
  pass; only a live test exercised the SSE parsing logic. Added
  `GeminiClientTests.StreamInteractionAsync_ParsesNamedSseEventsCorrectly` (client level) and
  `TranscriptionTests.StreamTranscribeAsync_SendsCorrectRequestShapeAndYieldsEvents` (service level).
- The ASP.NET Core sample never demonstrated this feature. TTS's own checklist required its `/speak`
  endpoint to be "actually run and hit with a real request, not just compiled" before considering that
  feature done; this feature's PR had skipped the equivalent. Added `GET /transcribe`, which
  round-trips through both audio features (speaks text via TTS, transcribes the result back), and
  actually ran it: `curl` against a live instance returned the correct transcript, `HTTP 200`.

**What this pass did not re-verify live:** the two streaming fixes (Bug 1, Bug 2) were validated by
precise unit tests asserting the exact corrected behavior (wire body content, mutation absence,
deferred-throw timing), not by a third live pass specifically targeting `StreamTranscribeAsync`. The
underlying SSE-reading mechanism itself was already live-verified twice before this review (research
phase and post-implementation phase); these fixes are narrow, mechanical corrections to object
lifecycle and exception timing, not to wire-protocol handling. Judged sufficient rather than asking
for the API key a fourth time in one session; noted here rather than left unstated.
