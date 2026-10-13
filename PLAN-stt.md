# Plan: Speech-to-text (STT) for Junaid.GoogleGemini.Net

Status: implemented and shipped in `6.6.0`. We verified the research live against the real API on
2026-09-07 (design, before any code). After implementation, we verified it live again in the same
session, through the actual `ITranscriptionService` and `IGeminiClient` code and not raw REST calls.
The second pass covered inline transcription, diarization with real distinct speaker labels,
file-based transcription through the existing Files API, streaming, and the client-side
`CustomVocabulary` plus `DiarizationMode` guard against the real API's rejection.

A deep second-pass self-review, done separately from the implementation pass, found and fixed three
real bugs before merge (§10). None of the first pass's own tests caught them. This is why a second
pass that you ask for by name matters, and is not a formality. A single pass with a green build and
green tests had already happened and reported success, and it still missed these bugs. After the
fixes, the full solution build had 0 warnings on all three targets. All 172 unit tests passed (16
new in total: 11 from implementation, and 5 more from the regression tests of the second pass). The
full live suite had 39 passed, 7 paid-tier skipped, and 0 failed. We added the new `/transcribe`
endpoint to the ASP.NET Core sample during the second pass, to match the `/speak` precedent of TTS.
We ran it and called it with a real request. It round-trips through TTS and STT together. We did not
only compile it.

## 1. Goal

Let a caller turn spoken audio into text with Google's dedicated `gemini-3.5-transcribe` model. That
includes its differentiated features: speaker diarization, word-level timestamps, custom vocabulary
biasing, and language auto-detection. This is the direct mirror of the TTS feature that shipped in
`6.5.0`. TTS turns text into audio. This turns audio into text.

## 2. The architecture fork, and why this plan looks different from PLAN-tts.md

TTS fit this library's existing `generateContent` shape exactly (see `PLAN-tts.md`). STT does not.
Two paths exist. They are different APIs, and they are not two flavors of one call:

Path A: plain audio understanding through `generateContent`. Any current chat model (we tested
`gemini-3.8-flash`) already accepts audio as an `inlineData` part. `GenerateWithImageAsync` already
uses the same mechanism for images. Send a prompt such as "Transcribe this audio exactly" plus the
audio bytes, and the model replies with the transcript as plain text. This library has no
audio-input plumbing today. `FileObject` (the type that `GenerateWithImageAsync` takes) is validated
as image-only at construction. But the underlying wire format is already `generateContent`, which
every part of this codebase already understands.

Path B: the dedicated `gemini-3.5-transcribe` model through the Interactions API. This is a newer,
different endpoint (`POST /v1beta/interactions`, not `models/{model}:generateContent`). It has its
own request shape, response shape, error shape, and streaming protocol. It is the only way to get
diarization, word timestamps, and custom vocabulary. Path A returns plain text with none of those.

`PLAN-tts.md` §9 looked at the Interactions API for TTS and walked away from it on purpose. We were
asked to choose here, and the decision went the other way: build Path B. It is the larger lift. We
accepted it, because diarization and timestamps are the real differentiators of this model, and
Path A cannot produce them at all. This section exists so that a future maintainer does not have to
work out again why this plan has such a different shape from the TTS plan that came right before it.

## 3. What we verified live, and how

We confirmed every request and response shape below with real REST calls against
`gemini-3.5-transcribe`. We did not rely on docs alone. This mattered. A first web search and two
doc-summarization passes each guessed at a response shape (`output_text` at the top level, speaker
labels such as `"spk_1"`, and a `"mode": {"type": "verbatim", ...}` object). Each guess turned out to
be wrong or unconfirmed, in the ways described below. We found the real shape by making the calls.

Test audio: real speech and not a synthetic tone. We generated it with this library's own shipped TTS
(`gemini-2.5-flash-preview-tts`) and decoded it to a real WAV file the same way that
`GeneratedAudio.ToWav()` does. We used two clips. One is a single-speaker sentence ("The quick brown
fox jumps over the lazy dog near the river bank"). The other is a two-speaker script (Joe and Jane,
multi-speaker TTS) for diarization testing.

### 3.1 Path A confirmed (`generateContent`, for context and comparison, not this plan's target)

```json
POST https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent
{
  "contents": [{"parts": [
    {"text": "Transcribe this audio exactly."},
    {"inlineData": {"mimeType": "audio/wav", "data": "<base64 wav>"}}
  ]}]
}
```
The call returned `HTTP 200`. It returned the plain-text transcript in
`candidates[0].content.parts[0].text` ("The quick brown fox jumps over the lazy dog near the
riverbank."). `usageMetadata` had an `AUDIO`-modality entry in `promptTokensDetails`, exactly like
every other multimodal call that this library already parses. This path needs no new response type.
We are not building it in v1 (see §4). But we had to confirm that it works, to make the Path A or B
decision honestly.

### 3.2 Path B confirmed (Interactions API, this plan's target)

Real endpoint and auth: `POST https://generativelanguage.googleapis.com/v1beta/interactions`. It
uses the same `x-goog-api-key` header that this library already sends everywhere else. No separate
auth setup is needed.

Real request shape, inline audio:
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
All field names use `snake_case`, unlike the `camelCase` of `generateContent`. This library already
requires an explicit `[JsonPropertyName]` on every property, whatever the endpoint. It has no global
naming policy to lean on. So this is a data point to get right per field. It is not a new mechanism
to build.

Real request shape, file reference instead of inline bytes: the same body, with
`{"type": "audio", "uri": "https://generativelanguage.googleapis.com/v1beta/files/...", "mime_type": "audio/wav"}`
in place of `data`. The `uri` is exactly what the existing `IFileService.UploadFileAsync` of this
library already returns as `FileResource.Uri`. So the Files API integration point is free. It needs
no new upload code. `ITranscriptionService` only needs to accept a `FileResource` or URI, in the
same way that `GenerateWithImageAsync` accepts a `FileObject`. We confirmed it live. We uploaded a
WAV through the existing resumable-upload flow and transcribed it by URI. The call returned `HTTP
200` and the correct transcript.

Real response shape, non-streaming:
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
The response has no top-level `output_text` field. A web search summary and a doc-fetch summary
each guessed that independently. The transcript lives at `steps[].content[].text`. It is in the same
place whether or not you requested diarization or timestamps. `annotations` is present only when you
set `timestamp_granularities`.

Real diarization shape, tested live on the two-speaker Joe and Jane clip, with `diarization_mode:
"speaker"` and `timestamp_granularities: ["word"]` together. The `annotations` entry of each word
gets a `"speaker": "spk:0"` or `"spk:1"` field (zero-indexed, colon-separated). This literal format
differs from the one that the docs guessed (`"spk_1"`, with an underscore and one-indexed). Getting
this exactly right matters for any helper that groups a diarized transcript.

`GET /v1beta/interactions/{id}` retrieves a completed interaction by ID. We tested it live
immediately after a POST. It is useful for polling. It has one real difference from the POST
response. The `steps` array of the GET response includes an extra leading `"type": "user_input"`
step that echoes what we sent (`{"uri": "...", "mime_type": "audio/wav", "type": "audio"}`). The
POST response does not include it. Any code that parses `Interaction` must treat `steps[].type` as a
real discriminator. It must not assume that every step is `model_output`.

Real streaming shape: `POST .../interactions?alt=sse` with `"stream": true` in the body. This is a
materially different SSE protocol than `streamGenerateContent?alt=sse`. This library already parses
that as one `data: {GenerateContentResponse}` event per chunk. Interactions streaming sends a
sequence of named events, each with a different shape:
```
event: interaction.created      data: {"interaction": {...status: "in_progress"...}}
event: interaction.status_update data: {"interaction_id": "...", "status": "in_progress"}
event: step.start               data: {"index": 0, "step": {"type": "model_output"}}
event: step.delta               data: {"index": 0, "delta": {"text": "...", "type": "text"}}
event: step.stop                data: {"index": 0}
event: interaction.completed    data: {"interaction": {...full object, status: "completed"...}}
event: done                     data: [DONE]
```
For the short test clip, the whole transcript arrived in a single `step.delta`. A longer clip would
presumably arrive as multiple deltas per step, but we did not test that with long audio (see the
open items in §7). A working streaming client needs a real parser for each event type. It cannot
reuse the existing single-shape SSE loop.

### 3.3 Real usage and cost quirk (read this before you wire cost governance)

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
`total_output_tokens` reads `0` in every single live call that we made in this session. That
includes calls that clearly produced real output text. The real output token count (14, in this
example) sits in `model_invocation_token_counts[].candidates_tokens_details[].tokens` instead. Note
also that `total_tokens` (147) equals `total_input_tokens` (147) exactly. The top-level total does not
include output tokens either. This looks like a copy-paste field-name bug in Google's own usage
accounting for this endpoint. It reproduced across nine separate live calls in this session (every
probe in the testing of §3.2). It is not a one-off glitch. If a future equivalent of `RecordSpend`
for this feature reads `total_output_tokens` directly, it will silently record $0 of output cost,
forever. Any usage adapter must read output tokens from `model_invocation_token_counts` and not from
the top-level `usage` fields.

This is also why `GeminiCostGovernor.RecordSpend(string?, UsageMetadata)` cannot be reused as is.
Its parameter type is the `UsageMetadata` shape of `generateContent` (`PromptTokenCount`,
`CandidatesTokenCount`, ...). The `usage` object of Interactions has different field names and has
the quirk above. We need a mapping function, and not a shared type. See §5.6.

### 3.4 Real error shape, and why it breaks the existing error path if you reuse it unmodified

```json
{"error": {"message": "custom_vocabulary is incompatible with diarization.", "code": "invalid_request"}}
```
The existing `ApiErrorResponse` and `ApiError` of this library (`Models/GoogleApi/ApiErrorResponse.cs`)
type `Code` as `int`. This matches the `generateContent` shape
`{"error": {"code": 404, "message": "...", "status": "NOT_FOUND"}}`. The `code` of Interactions is a
string enum (`"invalid_request"`, `"not_found"`, confirmed with a bad model name and a bad base64
payload). If you deserialize an Interactions error body with the existing `ApiErrorResponse` type,
it throws a `JsonException` on the int and string mismatch. `GeminiClient.HandleResponse` catches
that specific exception. It rethrows it as `GeminiSerializationException("Failed to parse API
response", ...)`, and discards the real, helpful message that the API sent back. Reusing the
existing `PostAsync` and `HandleResponse` pipeline unmodified for Interactions calls would turn every
clean 400 into an opaque serialization failure. We need a new error type and a new client method.
See §5.3.

The confirmed error messages are useful, and callers should see them directly:
- Bad model name: `"Model 'gemini-3.5-transcribe-nonexistent' not found. Did you mean 'gemini-3.5-transcribe'? ..."`
- Corrupt base64: `"The value is invalid for 'input[0].data'. Expected string, corrupt base64"`
- Missing model: `"Provide a 'agent', or 'model' parameter."`
- We confirmed live that the API really rejects `custom_vocabulary` (`HTTP 400`) when you combine it
  with `diarization_mode` or (by the same message) `timestamp_granularities`. This matches Google's
  docs. The library should validate this client-side before the call. The API would return a 400
  otherwise.

### 3.5 Confirmed field acceptance (schema only; we did not fully verify the behavioral effect)

We sent each of these in `generation_config.transcription_config`, and each returned `HTTP 200`. A
made-up field name returns an `"Unknown parameter"` `400` instead. That is how we caught the two
rejected guesses below.
- `language_codes: ["en-US"]`: accepted.
- `custom_vocabulary: ["Gemini"]`: accepted alone. Rejected when combined with diarization (§3.4).
- `diarization_mode: "speaker"`: accepted. It produces real per-word `speaker` labels (§3.2).
- `timestamp_granularities: ["word"]`: accepted. It produces real `start_offset` and `end_offset`
  values (§3.2).
- `timestamp_granularities: ["segment"]` and `["word", "segment"]`: both accepted without error. We
  did not confirm that the segment-level output actually differs, because the test clip was one
  short sentence.
- `mode: "smart"` and `mode: {"type": "verbatim"}`: both accepted without error. We did not confirm
  that either changes the output for a clean, short test clip. Re-verify the behavior before you
  document this field as load-bearing. Or drop it from v1 and let a caller pass it through opaquely
  if they want it.

Rejected as fields (confirms that they are not real top-level `transcription_config` keys):
`diarization_config` (a nested object), `enable_diarization`, and `bogus_field_xyz`. The Interactions
API does validate unknown parameters and says so clearly. That made this kind of probing reliable.

## 4. Explicit scope

In scope (v1):
- `TranscribeAsync`: single-call transcription of inline audio bytes or a Files API URI, against
  `gemini-3.5-transcribe`, through the Interactions API.
- Diarization (`DiarizationMode`), word timestamps (`TimestampGranularities`), custom vocabulary
  (`CustomVocabulary`), and language hints (`LanguageCodes`) as request options. We verified all of
  them live in §3.2 and §3.5.
- Client-side validation that `CustomVocabulary` is not combined with `DiarizationMode` or
  `TimestampGranularities`. It throws a clear `ArgumentException` before the call, and does not let
  the API return a 400 (confirmed real API behavior, §3.4).
- A new `Interaction` response type with `Text()` and `TryGetText()` (they concatenate the text of
  `model_output` steps and mirror `GenerateContentResponse.Text()`). It also has a `Words()` helper
  that flattens the `word_info` annotations, including `Speaker` when you requested diarization.
- `StreamTranscribeAsync`: real incremental streaming that parses the named-event SSE protocol from
  §3.2. It is not a fake wrapper around the non-streaming call.
- `GetInteractionAsync(id)`: wraps the confirmed `GET /v1beta/interactions/{id}`, for polling.
- Cost governance: a `gemini-3.5-transcribe` pricing entry, and a usage-mapping function that reads
  the real output-token location from §3.3 and not the misleading top-level field.
- It reuses the existing `IFileService` unchanged for the file-URI input path (§3.2). It needs no
  new upload code.

Out of scope for this change (do not implement):
- Path A (plain-text transcription through `generateContent` on any chat model). It works (§3.1),
  and it needs no Interactions API. We evaluated it and passed it over for v1, in favor of the
  diarization and timestamps of the dedicated model, which are the actual point of this feature.
  Revisit it only if the Interactions build of v1 turns out to be a bad bet (see the reasoning in
  §2). Do not build both paths at once.
- `gemini-3.5-transcribe-live` (the Live API, the WebSocket streaming variant). It uses a separate
  protocol entirely (WebSocket, and not SSE over HTTP). The existing "Live API's bidirectional
  audio" backlog item in `ROADMAP.md` already covers that ground. Do not fold it into this feature.
- Batch transcription of many files in one call. The Interactions API has no batch endpoint as of
  this writing (confirmed with two independent doc sources). We did not test it live, because there
  is nothing to call. A caller who needs to transcribe many files loops over `TranscribeAsync` for
  now.
- Audio longer than a few seconds. We did not test it. Google's docs claim up to 1 hour per request
  (30 minutes when diarization or timestamps are on). They also say that the call may not return
  synchronously for long audio. We did not test this: does a long request block until it is done,
  or return `"status": "in_progress"` and require a `GetInteractionAsync` poll loop? Confirm this
  before shipping. Every live call in this plan returned `"status": "completed"` at once, because
  every test clip was a few seconds long.
- The 85+ language codes and the diarization speaker cap (8, per the docs) as C# constants. The
  reasoning is the same as for the voice names of TTS (`PLAN-tts.md` §3). Use plain strings and not
  an enum, so that new codes and limits from Google do not require a library update.
- `mode` (`"smart"` or `"verbatim"`) as a strongly typed option. The API accepts it (§3.5), but we
  did not confirm its actual behavioral effect. If you include it at all, expose it as a
  passthrough `string?`. Do not build named constants around unverified behavior.

## 5. Architecture

### 5.1 New model types (`Models/GoogleApi/`, flat, matching the existing Batch and File convention)

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

All new root types need `[JsonSerializable(typeof(...))]` entries in `GeminiJsonContext.cs`, like
every other root type (see the existing list at `GeminiJsonContext.cs:25-48`). The generator
discovers nested types under a registered root automatically, as it did for the children of
`SpeechConfig` in TTS.

### 5.2 `GeminiApiException` gets a new optional field, and not a new exception type

Do not invent a parallel exception type. Add one nullable property:
```csharp
/// <summary>Machine-readable error code from the Interactions API (e.g. "invalid_request"), if this
/// exception came from that API. Null for generateContent-sourced exceptions; use <see cref="Status"/>
/// for those instead.</summary>
public string? InteractionErrorCode { get; }
```
Keep `ApiError`, `Status`, and `ErrorCode` meaning exactly what they already mean for
`generateContent`. Do not force the string `code` of Interactions into `ApiError.Code` (which is
`int`). Do not overload the existing meaning of `ApiError.Status`. `StatusCode` (the HTTP status)
already covers what the Interactions error body does not provide separately.

### 5.3 New `GeminiClient` methods, and no reuse of `PostAsync` and `StreamAsync`

Add two new methods on `IGeminiClient` and `GeminiClient`. They are parallel to the existing
`PostAsync` and `StreamAsync`, and adapted for the three differences that we confirmed (§3.2 to
§3.4): the error body shape, the response type, and the model tagging of telemetry.
`GeminiTelemetry.Parse` extracts the model from a `models/{model}:...` URL segment. Interactions
requests do not have that segment. The model lives in the JSON body. So you must pass it through
explicitly, and not parse it from the endpoint string.

```csharp
Task<Interaction> PostInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default);
IAsyncEnumerable<InteractionStreamEvent> StreamInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default);
```

`StreamInteractionAsync` parses the real named-event protocol from §3.2 (`interaction.created`,
`step.delta`, `interaction.completed`, and so on). It does not reuse the single-shape SSE loop that
`StreamCoreAsync` already has. A new small `InteractionStreamEvent` type (the event name plus
whichever payload it carries) is the cleanest type to yield. Accumulating those events into a
running transcript is a job for `TranscriptionService`, and not for the client.

### 5.4 New `ITranscriptionService`, a sibling to `IFileService` and `IBatchService`, and not folded into `IGeminiService`

The doc comment of `IGeminiService` calls it "the unified service interface for all Gemini content
generation operations." Every method on it returns or streams `GenerateContentResponse`. If we added
a method that returns `Interaction`, it would break that contract for no benefit. This codebase
already has a precedent for a focused sibling interface per distinct concern (`IFileService`,
`IBatchService`, `ICachingService`). Each one is registered in the same way in `GeminiExtensions.cs`
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

Add a new `gemini-3.5-transcribe` pricing entry: $2.00 in and $12.00 out per 1M tokens, with a free
tier. The numbers came from a single fetch of the pricing page and not from a live billing check.
Re-verify them before merging, with the same standing caveat as every other pricing entry.

This needs a small usage-mapping function and not a reuse of `UsageMetadata`. `InteractionUsage`
has different field names than the `UsageMetadata` of `generateContent`. Also, per §3.3, you must
sum the output tokens from `ModelInvocationTokenCounts[].CandidatesTokensDetails[].Tokens`. Do not
read them directly from `InteractionUsage.TotalOutputTokens`. Decide at implementation time whether
the mapping produces a `UsageMetadata` instance (which reuses the existing signature of
`RecordSpend`) or gets a small parallel equivalent of `RecordSpend`. Either way, put the mapping
function and its rationale in code comments next to wherever it lives. The quirk is very
non-obvious.

### 5.7 Telemetry

You cannot reuse `GeminiTelemetry.Parse(endpoint)` unmodified. It extracts the model from a
`models/{model}:operation` URL segment. Interactions requests have neither. The `endpoint` is just
`"interactions"`, and the model is in the request body. `PostInteractionAsync` and
`StreamInteractionAsync` must pass the model explicitly to `GeminiTelemetry.StartOperation`,
`RecordUsage`, and `RecordDuration`. They must not rely on URL parsing.

## 6. Naming and design decisions worth recording

- `ITranscriptionService`, and not a method on `IGeminiService`. See §5.4. `Interaction` is not a
  `GenerateContentResponse`. Folding it in would break the stated contract of that interface.
- New `PostInteractionAsync` and `StreamInteractionAsync`, and no reuse of `PostAsync` and
  `StreamAsync`. See §3.4, §3.2, and §5.3. The error shape, response type, and telemetry model
  tagging all differ. If we reused the existing generic methods as is, they would misbehave
  silently. The caller would get an opaque `GeminiSerializationException` and not the API's real
  message. Telemetry spans would have no model tag.
- A new field on `GeminiApiException`, and not a new exception type. See §5.2. This keeps one
  exception type for both APIs, which is simpler for callers who write a single `catch`. It does not
  corrupt what `ApiError` already means for `generateContent`.
- No enum for `DiarizationMode`, `TimestampGranularities`, language codes, or custom vocabulary. The
  reasoning is the same as this library already applies to TTS voice names (`PLAN-tts.md` §3) and to
  model names in general. Use plain strings, so that new values from Google do not require a
  library update.
- We deliberately did not build Path A (`generateContent` audio understanding) next to Path B. See
  §2 and §4. We confirmed that it works. But building both at once means that we ship two ways to do
  the same thing, with different capability levels. For this use case, Path B is a strict superset
  of what Path A can produce. So there is no reason to ship the weaker path, unless the larger lift
  of Path B turns out to be a mistake.

## 7. Open items to verify before you write code (do not skip)

- [x] Diarization produces real, distinct speaker labels through the typed client. The raw API
      accepted it earlier. Resolved. The live test
      `TranscribeAsync_WithDiarizationAndTimestamps_ReturnsDistinctSpeakerLabels` confirms at least 2
      distinct `"spk:N"` labels and real `StartOffset` and `EndOffset` values on a real two-speaker
      clip. It goes through `ITranscriptionService` and `Interaction.Words()`, and not raw REST.
- [x] The client-side `CustomVocabulary` plus `DiarizationMode` guard actually matches the real API's
      rejection. The earlier raw-REST probe suggested it. Resolved. The live
      test `PostInteractionAsync_CustomVocabularyWithDiarization_RealApiRejectsWithInvalidRequest`
      bypasses `TranscriptionOptions.Validate()` and calls `IGeminiClient.PostInteractionAsync`
      directly with the conflicting combination. It confirms a real `HTTP 400` and
      `"invalid_request"`.
- [ ] Long audio, synchronous or not. Still open. Every live call that we made in this session
      (research and post-implementation both) used a clip a few seconds long and got `"status":
      "completed"` at once. Google's docs claim up to 1 hour of audio per request.
      `ITranscriptionService` has `GetInteractionAsync` for polling, in case long audio needs it. We
      never exercised it against real long audio. So we have not confirmed whether it is actually
      needed.
- [ ] Inline `data` size limit. Still open. We did not test a large payload. Do not assume that the
      informal ~20MB inline guidance of `generateContent` applies to Interactions.
      `ITranscriptionService.TranscribeFileAsync` (the Files API path) exists as the documented
      escape hatch. But we did not establish a threshold for when to prefer it.
- `mode` field. Resolved by scoping it out. We did not implement it in v1 (`TranscriptionConfig` has
  no `Mode` property), per the explicit scope decision in §4. Revisit it only when you have a real,
  confirmed behavior to build a typed option around.
- [ ] The actual output shape of `timestamp_granularities: ["segment"]`. Still open.
      `TranscriptionConfig.TimestampGranularities` accepts any string list (it is not locked to an
      enum, see §6). A caller can already pass `"segment"` today. This library's own tests exercised
      only `"word"`.
- [ ] The actual bias effect of custom vocabulary. Still open. We confirmed it live as an accepted
      field. We also confirmed that it is incompatible with diarization (§3.4). But we never tested
      whether it actually changes the recognition of an unusual term.
- [ ] Streaming with a long clip. Still open. Every streaming test that we made in this session
      (research and post-implementation) returned the entire transcript as a single `step.delta`.
      We have not confirmed whether a long clip arrives as multiple deltas.

## 8. Tests to add (all implemented; we left the remaining open items from §7 as they are and did not force a test)

Unit tests (`tests/Junaid.GoogleGemini.Net.Tests/TranscriptionTests.cs`), based on
`FakeHttpMessageHandler`, 11 tests:
- [x] `TranscribeAsync` sends the correct `InteractionRequest` shape for inline audio, and for a file
      URI.
- [x] `TranscriptionOptions` throws `ArgumentException` when `CustomVocabulary` is combined with
      `DiarizationMode` or `TimestampGranularities`. It does so without a network call.
- [x] A fake Interactions error body (`{"error": {"code": "invalid_request", "message": "..."}}`)
      deserializes correctly. It surfaces through `GeminiApiException.InteractionErrorCode` and
      `.Message`, and not through a `GeminiSerializationException`.
- [x] The usage-mapping function reads output tokens from `ModelInvocationTokenCounts`. A fake
      response with `TotalOutputTokens: 0` but real `CandidatesTokensDetails` still prices the output
      cost correctly. This is the regression test for the quirk in §3.3.
- [x] `Interaction.Text()` and `Words()` skip a `"user_input"` step and aggregate only `"model_output"`
      steps.

Live tests (`tests/Junaid.GoogleGemini.Net.IntegrationTests/TranscriptionLiveTests.cs`),
`[RequiresGeminiKey]`, 5 tests:
- [x] `TranscribeAsync` against real inline audio returns the expected transcript. The audio came
      from this library's own TTS, the same pattern that this plan used in its research.
- [x] `TranscribeAsync` with `DiarizationMode` and `TimestampGranularities`, against a real
      two-speaker clip, returns per-word `Speaker` labels that actually distinguish the two
      speakers.
- [x] `PostInteractionAsync` with `CustomVocabulary` and `DiarizationMode` together, called directly
      (and bypassing the client-side guard) against the real API, confirms a real `HTTP 400` and
      `"invalid_request"`. This matches what the guard prevents.
- [x] `TranscribeFileAsync` against a file uploaded through the existing `IFileService` succeeds.
- [x] `StreamTranscribeAsync` yields events and a final completed `Interaction` with `Usage`.
- [ ] Long-audio behavior (§7): not tested. No long audio sample was available in this session. We
      left it as an explicit open item and did not skip it silently.

Full suite results at the time of shipping: 167 of 167 unit tests passed (11 new). The full solution
build had 0 warnings on all three targets. The complete live suite had 39 passed, 7 paid-tier
skipped, and 0 failed. That covers the 5 new STT tests and every existing live test, which confirms
no regression.

## 9. Docs to update

- [x] Add `docs/articles/transcription.md`, following the structure of `docs/articles/tts.md`.
- [x] Add it to `docs/articles/toc.yml` and to the guide list in `docs/index.md`.
- [x] `README.md`: add a new "Speech-to-text" bullet and a short code sample, matching the format of
      the TTS section.
- [x] `Junaid.GoogleGemini.Net.csproj`: add a `6.6.0` entry to `PackageReleaseNotes`.
- [x] `ROADMAP.md`: add a `6.6.0` shipped-feature entry. There was no explicit STT-shaped backlog
      item to remove (we confirmed that by checking, see the original note in this section). Also
      annotate the existing TTS-era backlog item for the Interactions API. It should point to §2 of
      this plan, which explains why STT answered "should this use the Interactions API" the opposite
      way from TTS.
- [x] Write everything in the style of this repo's `CLAUDE.md`: short sentences, no em dashes. We
      checked this with a literal grep across every new and changed file before publishing, and not
      only with a visual scan.

## 10. Deep second-pass review: three real bugs found and fixed before merge

We asked explicitly for a second, separate review pass after the implementation was already built,
tested (167 of 167 unit tests, 39 of 39 non-skipped live tests), and in a PR. We re-read every
changed file fresh, as if we saw it for the first time. We found three real bugs that none of the
first pass's own tests caught. A green build and passing tests did not catch any of them. A test
suite catches this kind of bug only if someone thinks to write the specific test for it. That is
exactly what a second pass is for.

Bug 1: `StreamInteractionAsync` changed the caller's own `InteractionRequest` object. It set
`request.Stream = true;` directly on the object that the caller passed in, and not on a copy. This
library's own established convention exists to avoid exactly this (see the doc comment of
`GenerateAudioAsync` and `StreamAudioAsync`: "on a cloned options object"). Suppose a caller built
one `InteractionRequest` and reused it for both a `PostInteractionAsync` call and a
`StreamInteractionAsync` call. The non-streaming call would silently have had `"stream": true` too,
because the same object got changed in place. We fixed it to clone the request. Regression test:
`GeminiClientTests.StreamInteractionAsync_DoesNotMutateCallersRequestObject`.

Bug 2: `StreamTranscribeAsync` was not an async iterator. Its validation threw synchronously at call
time and not on the first `MoveNextAsync()`. Every other method in this codebase that returns an
`IAsyncEnumerable` (`StreamAsync`, `StreamWithImageAsync`, `StreamAudioAsync`) is an
`async IAsyncEnumerable` iterator (`yield return`). It defers everything, including argument
validation, to the first enumeration. `StreamTranscribeAsync` instead ran its validation at once and
returned the downstream enumerable directly. A caller who wraps only the `await foreach` in a
try/catch would have had a validation `ArgumentException` escape uncaught. That is the natural
pattern, given how every other streaming method here behaves. We fixed it to a real iterator method.
Regression test: `TranscriptionTests.StreamTranscribeAsync_DefersValidationToFirstMoveNext_NotAtCallTime`.

Bug 3 (minor, with no observable effect, and we fixed it anyway): the `TotalTokenCount` of
`MapUsage` excluded thought tokens. The real `totalTokenCount` of `generateContent` is prompt +
candidates + thoughts (confirmed from a live call earlier in this session: 8 + 2 + 96 = 106). The
mapper computed only prompt + candidates. This is harmless today, because nothing in `ComputeCost`,
`RecordSpend`, or telemetry reads `TotalTokenCount`. They read only the individual fields. But it
would be wrong if that ever changes, or if a caller ever inspects the mapped value directly. We
fixed it. We also added an explicit code comment that flags a gap. The assumption that
`CachedContentTokenCount` is a subset of the input (borrowed from the semantics of
`generateContent`) was never exercised live, because every observed `TotalCachedTokens` was `0`.
Transcription requests have no way to attach cached content today. Regression test:
`GeminiClientTests.PostInteractionAsync_MapsUsage_ReadingOutputTokensFromModelInvocationTokenCounts_NotTheMisleadingTopLevelField`
asserts the corrected value directly.

The same pass also found two real gaps that were not bugs. We fixed both:
- No unit test covered `StreamTranscribeAsync` or `StreamInteractionAsync` before this pass. Only a
  live test exercised the SSE parsing logic. We added
  `GeminiClientTests.StreamInteractionAsync_ParsesNamedSseEventsCorrectly` (at the client level) and
  `TranscriptionTests.StreamTranscribeAsync_SendsCorrectRequestShapeAndYieldsEvents` (at the service
  level).
- The ASP.NET Core sample never demonstrated this feature. The checklist of TTS required its
  `/speak` endpoint to be "actually run and hit with a real request, not just compiled" before we
  considered that feature done. The PR for this feature had skipped the equivalent. We added
  `GET /transcribe`, which round-trips through both audio features: it speaks text through TTS and
  transcribes the result back. We ran it. `curl` against a live instance returned the correct
  transcript with `HTTP 200`.

What this pass did not re-verify live: we validated the two streaming fixes (Bug 1 and Bug 2) with
precise unit tests that assert the exact corrected behavior (the wire body content, the absence of
mutation, and the timing of the deferred throw). We did not run a third live pass that targeted
`StreamTranscribeAsync` specifically. Two earlier live passes (the research phase and the
post-implementation phase) had already verified the underlying SSE-reading mechanism. These fixes
are narrow, mechanical corrections to object lifecycle and exception timing. They do not touch
wire-protocol handling. We judged that sufficient, and we did not ask for the API key a fourth time
in one session. We note it here so that it is not left unstated.
