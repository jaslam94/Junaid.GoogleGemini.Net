# Plan: Text-to-speech (TTS) for Junaid.GoogleGemini.Net

**Status:** Implemented and shipped in `6.5.0`. Live-verified against the real API on 2026-09-04
(design), 2026-09-06 (all three model constants, post-implementation), and again on 2026-09-06
against a fresh billing-enabled key (all 5 live tests plus the full 41-test live suite). Also
evaluated and deliberately deferred a newer "Interactions API" surface Google now documents
alongside `generateContent`; see §9.

## 1. Goal

Let a caller turn text into spoken audio using Gemini's native TTS models, in the same low-friction
style as `GenerateImageAsync`. Two modes: single speaker (one voice for the whole text) and multi
speaker (a script with named speakers, each given a different voice).

## 2. What was verified live, and how

Everything below was confirmed with real REST calls against `gemini-2.5-flash-preview-tts`, not
read from docs alone. This matters: a first web search described a different, newer "Interactions
API" shape (`input`, `response_format`, `interaction.output_audio.data`) that does not match this
library's `generateContent`-based architecture at all. That shape was wrong for this feature and was
discarded before any code was written.

**Confirmed real model IDs:**
- `gemini-2.5-flash-preview-tts`
- `gemini-2.5-pro-preview-tts`
- `gemini-3.1-flash-tts-preview`

All three are `-preview` models today. Live-tested only `gemini-2.5-flash-preview-tts` directly;
the other two share the same documented request and response shape and are added on that basis, not
independently confirmed. Re-check before shipping if that matters to a release.

**Confirmed real request shape**, single speaker:
```json
{
  "contents": [{"parts": [{"text": "Say cheerfully: Have a wonderful day!"}]}],
  "generationConfig": {
    "responseModalities": ["AUDIO"],
    "speechConfig": {
      "voiceConfig": {"prebuiltVoiceConfig": {"voiceName": "Kore"}}
    }
  }
}
```

**Confirmed real request shape**, multi speaker:
```json
{
  "contents": [{"parts": [{"text": "TTS the following conversation between Joe and Jane: ..."}]}],
  "generationConfig": {
    "responseModalities": ["AUDIO"],
    "speechConfig": {
      "multiSpeakerVoiceConfig": {
        "speakerVoiceConfigs": [
          {"speaker": "Joe", "voiceConfig": {"prebuiltVoiceConfig": {"voiceName": "Kore"}}},
          {"speaker": "Jane", "voiceConfig": {"prebuiltVoiceConfig": {"voiceName": "Puck"}}}
        ]
      }
    }
  }
}
```

Both shapes returned `HTTP 200` with a real, valid response on the live call.

**Confirmed real response shape**, `candidates[0].content.parts[0].inlineData`:
```json
{
  "mimeType": "audio/L16;codec=pcm;rate=24000",
  "data": "<base64 PCM bytes>"
}
```

The exact `mimeType` string, `audio/L16;codec=pcm;rate=24000`, was read directly from a live
response. This is raw 16-bit linear PCM at 24000 Hz, not a WAV or MP3 file. A caller who writes
`Data` straight to a `.wav` file gets an unplayable file; a real WAV header must be added first (see
§4.3).

**Update, 2026-09-06, post-implementation:** the assumption two paragraphs up (that the other two
models "share the same documented request and response shape") turned out to be half right. The
*request* shape is shared, confirmed by actually calling all three models through the real
`GenerateAudioAsync`. The *response* `mimeType` is not: `gemini-2.5-flash-preview-tts` and
`gemini-2.5-pro-preview-tts` both return `audio/L16;codec=pcm;rate=24000` (capital L, no spaces,
explicit `codec=pcm`), but `gemini-3.1-flash-tts-preview` returns
`audio/l16; rate=24000; channels=1` instead: lowercase, spaced, no `codec=pcm` segment, and an
explicit channel count in its place. A first version of `GeneratedAudio.ToWav()` was built from only
the first model tested and threw `FormatException` on this real response the first time
`gemini-3.1-flash-tts-preview` was actually called. Fixed to parse the sample rate and (optional)
channel count independently from wherever they appear in the string, rather than matching one rigid
whole-string shape. This is exactly the kind of gap "the other two share the same shape, not
independently confirmed" was flagged for in the first place; it just took an actual call, not a
docs read, to find precisely where the assumption broke.

Confirmed mono channel count and 16-bit sample width from Google's own TTS guide prose, not
independently verified live (the response gives no explicit channel-count field to check).

**Confirmed real usage accounting.** Audio output tokens surface through the existing
`UsageMetadata.CandidatesTokensDetails` modality breakdown (added in `6.4.1`), not a new field:
```json
"usageMetadata": {
  "promptTokenCount": 8,
  "candidatesTokenCount": 51,
  "promptTokensDetails": [{"modality": "TEXT", "tokenCount": 8}],
  "candidatesTokensDetails": [{"modality": "AUDIO", "tokenCount": 51}]
}
```
This means cost governance needs no new plumbing to see audio token counts. It only needs pricing
entries for the TTS models.

**Confirmed streaming works.** `streamGenerateContent?alt=sse` against the same model returned
`HTTP 200` with the same `inlineData` shape inside the streamed chunk, and a final chunk carrying
`usageMetadata`, matching every other streaming response this library already parses. No special
handling is needed beyond registering the TTS models; the existing `StreamAsync` path applies as-is.
For the short test text used, the whole clip came back in a single SSE chunk; a longer script may
arrive in more than one, and the existing `StreamCoreAsync` chunk-accumulation logic already handles
that correctly for any part type.

**Not independently confirmed, taken from Google's docs and pricing page (verify before merging):**
- Pricing: `gemini-2.5-flash-preview-tts` $0.50 in / $10.00 out per 1M tokens, free tier available.
  `gemini-2.5-pro-preview-tts` $1.00 in / $20.00 out, paid tier only. `gemini-3.1-flash-tts-preview`
  $1.00 in / $20.00 out, free tier available. These numbers came from a single fetch of the pricing
  page, not a live billing check. Re-verify before merging, same as every other pricing entry in
  `GeminiCostGovernor.DefaultPricing`.
- 30 named voices (Kore, Puck, Charon, Zephyr, and 26 more). Full list lives in Google's docs, not
  duplicated as constants here (see §5 for why).
- 32k token context window, and a caveat that quality can drift on clips longer than a few minutes.

## 3. Explicit scope

**In scope (v1):**
- Single-speaker generation: one voice for the whole input text.
- Multi-speaker generation: a script with named speakers, each mapped to a voice.
- A new `GeneratedAudio` response type with a `ToWav()` helper that adds a correct WAV header, since
  the raw PCM Google returns is not directly playable.
- Non-streaming (`GenerateAudioAsync`) and streaming (`StreamAudioAsync`), mirroring the
  `GenerateImageAsync` / existing `StreamAsync` split.
- Cost governance pricing entries for all three TTS models, using the numbers in §2, re-verified
  before merging.

**Explicitly out of scope for this change (do not implement):**
- The 30 voice names as C# constants. Model names in this library are already not validated against
  an allow-list, specifically so new models work without a library update (see `GeminiConstants.Models`'
  own doc comment). Voice names get the same treatment: a plain `string`, not an enum. Hardcoding 30
  names that Google can add to or rename invites exactly the kind of staleness this library avoids
  elsewhere.
- Style control via inline tags (`[whispers]`, `[excited]`) or structured "Director's Notes" prompts.
  These are plain text conventions the caller can already use today by writing them into the prompt
  string; there is no separate API surface for them to wrap.
- The Live API's bidirectional audio (separate from this feature; already tracked as its own,
  unstarted item in `ROADMAP.md`).
- Any retry logic for the "occasional random text token" issue Google's docs mention. That is a
  model-quality caveat to document, not a client-side workaround to build.

## 4. Architecture

### 4.1 New model types (`Models/GoogleApi/GenerationConfig.cs`, alongside `ImageConfig`)

```csharp
public class SpeechConfig
{
    public VoiceConfig? VoiceConfig { get; set; }
    public MultiSpeakerVoiceConfig? MultiSpeakerVoiceConfig { get; set; }
}

public class VoiceConfig
{
    public PrebuiltVoiceConfig? PrebuiltVoiceConfig { get; set; }
}

public class PrebuiltVoiceConfig
{
    public string? VoiceName { get; set; }
}

public class MultiSpeakerVoiceConfig
{
    public List<SpeakerVoiceConfig>? SpeakerVoiceConfigs { get; set; }
}

public class SpeakerVoiceConfig
{
    public string? Speaker { get; set; }
    public VoiceConfig? VoiceConfig { get; set; }
}
```

Add `SpeechConfig` as a new property on `GenerationConfig`, next to `ImageConfig`.

### 4.2 `GeminiRequestOptions` additions, mirroring `ImageAspectRatio`/`ImageSize`

```csharp
/// <summary>Voice for single-speaker TTS (see Google's TTS voice list). Gemini TTS models only.</summary>
public string? VoiceName { get; set; }

/// <summary>Speaker-to-voice map for multi-speaker TTS. Gemini TTS models only. When set, this
/// takes priority over <see cref="VoiceName"/>.</summary>
public IReadOnlyList<(string Speaker, string VoiceName)>? SpeakerVoices { get; set; }
```

`RequestFactory.CreateGenerationConfig` gets a new `BuildSpeechConfig(options)` private method,
called and assigned the same way `BuildImageConfig` is: returns `null` when neither field is set, a
single-speaker `SpeechConfig` when only `VoiceName` is set, and a multi-speaker one when
`SpeakerVoices` is set (which wins if both are set, since a script naming multiple speakers cannot
sensibly also use one global voice).

Add both new properties to `GeminiRequestOptions.Clone()`, same as every other property already
there.

### 4.3 `GeneratedAudio` response type and `GenerateContentResponse` accessors

Mirrors `GeneratedImage`/`Images()`/`TryGetImages()`/`GetImagesOrThrow()` exactly, plus one addition
these do not need: a way to make the raw PCM playable.

```csharp
public sealed class GeneratedAudio
{
    /// <summary>The API's exact mimeType, e.g. "audio/L16;codec=pcm;rate=24000".</summary>
    public required string MimeType { get; init; }

    /// <summary>Raw decoded audio bytes, in the format <see cref="MimeType"/> describes. This is
    /// NOT a playable file on its own; call <see cref="ToWav"/> for that.</summary>
    public required byte[] Data { get; init; }

    /// <summary>Wraps <see cref="Data"/> in a minimal 44-byte WAV header, producing a file most
    /// players and browsers can open directly. Parses the sample rate from <see cref="MimeType"/>;
    /// assumes 16-bit mono PCM, matching every Gemini TTS response confirmed to date. Throws
    /// <see cref="FormatException"/> if <see cref="MimeType"/> is not the expected
    /// "audio/L16;codec=pcm;rate=NNNN" shape (e.g. a future model returning a different codec).</summary>
    public byte[] ToWav();
}
```

`GenerateContentResponse` gets `Audio()`, `TryGetAudio(out GeneratedAudio? audio)`, and
`GetAudioOrThrow()`, filtering `inlineData` parts to `MimeType` starting with `"audio/"` (mirroring
`Images()`'s `"image/"` filter exactly). Since a TTS response has exactly one audio part per call (no
multi-clip responses observed or documented), these return a single `GeneratedAudio?`, not a list,
unlike the plural `Images()`.

### 4.4 `IGeminiService` methods

```csharp
Task<GenerateContentResponse> GenerateAudioAsync(
    string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default);

IAsyncEnumerable<GenerateContentResponse> StreamAudioAsync(
    string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default);
```

Both are thin wrappers, exactly like `GenerateImageAsync`: fill in `Model ??= Models.RecommendedTts`
and `ResponseModalities ??= [GeminiConstants.ResponseModalities.Audio]` on a cloned options object,
then delegate to the existing internal generate/stream path with a distinct operation label ("audio
generation") for accurate logs and errors, the same reasoning already documented on
`GenerateInternalAsync`.

### 4.5 New constants (`GeminiConstants`)

```csharp
// In Models:
public const string Gemini25FlashTts = "gemini-2.5-flash-preview-tts";
public const string Gemini25ProTts = "gemini-2.5-pro-preview-tts";
public const string Gemini31FlashTts = "gemini-3.1-flash-tts-preview";
public static string RecommendedTts => Gemini25FlashTts; // cheaper of the two GA-shaped options, free tier

// In ResponseModalities:
public const string Audio = "AUDIO";
```

### 4.6 Cost governance

Add three entries to `GeminiCostGovernor.DefaultPricing`, using the numbers in §2 once re-verified.
No new code path is needed. `RecordSpend`/`ComputeCost` already price `CandidatesTokenCount` at the
model's `OutputPerMillionTokensUsd` rate; audio tokens flow through that unchanged, since Google
already counts them as regular `candidatesTokenCount` (confirmed in §2), just with an `AUDIO`
modality tag on the breakdown. Document in the pricing table's surrounding comment (matching the
existing note on image models' text-vs-image output rates) that this prices 100% of output at the
audio rate, which is correct for TTS since a TTS response has no separate text output to
under-or-over-count, unlike the image models.

### 4.7 Telemetry

No new code needed. `GeminiTelemetry.RecordUsage`/`RecordDuration` already instrument every
`PostAsync`/`StreamAsync` call by operation name and model; `GenerateAudioAsync`/`StreamAudioAsync`
inherit this for free through the shared internal path, the same way `GenerateImageAsync` already
does.

## 5. Naming and design decisions worth recording

- **No voice-name constants.** See §3. A caller passes a plain string for `VoiceName`; typos are the
  API's problem to reject, not this library's to prevent.
- **Tuple list for `SpeakerVoices`, not a dictionary.** A `Dictionary<string, string>` would work for
  the common case but silently drops order and cannot represent the same speaker appearing twice
  with different config in some hypothetical future API shape. A `List<(string, string)>` costs
  nothing extra and keeps input order, which is what Google's own multi-speaker example preserves.
- **`ToWav()` on `GeneratedAudio`, not a free function.** Keeps the decode-and-make-playable path
  discoverable from the type itself, matching how `GeneratedImage`'s `Data` is already directly
  usable without a separate helper class to find.
- **Singular `Audio()`, plural `Images()`.** Deliberate, not an inconsistency: Gemini's image models
  can return multiple images per call; nothing observed or documented suggests TTS ever returns more
  than one audio clip per call.

## 6. Tests to add

**Unit** (`tests/Junaid.GoogleGemini.Net.Tests`), `FakeHttpMessageHandler`-based:
- `GenerateAudioAsync` sends `responseModalities: ["AUDIO"]` and the correct single-speaker
  `speechConfig` shape when `VoiceName` is set.
- `GenerateAudioAsync` sends the correct multi-speaker `speechConfig` shape when `SpeakerVoices` is
  set, and that it wins when both `VoiceName` and `SpeakerVoices` are set.
- `GenerateContentResponse.Audio()`/`TryGetAudio()`/`GetAudioOrThrow()` decode a fake `inlineData`
  part correctly and return `null`/throw appropriately when no audio part is present.
- `GeneratedAudio.ToWav()` produces a byte array with a valid 44-byte RIFF/WAVE header for a known
  `mimeType`/`Data` pair, and throws `FormatException` for a `mimeType` that does not match the
  expected shape.

**Live** (`tests/Junaid.GoogleGemini.Net.IntegrationTests`), `[RequiresGeminiKey]`:
- Single-speaker `GenerateAudioAsync` against a real key returns audio that decodes and whose `ToWav()`
  output starts with a valid `RIFF`/`WAVE` header.
- Multi-speaker `GenerateAudioAsync` against a real key succeeds (same shape check).
- `StreamAudioAsync` against a real key yields at least one chunk carrying an audio part, and the
  final chunk carries `UsageMetadata` with an `AUDIO` modality entry.
- Confirm live whether `gemini-2.5-pro-preview-tts` genuinely requires a paid-tier key (per §2's
  pricing-page claim); gate that one test with `[RequiresPaidGeminiKey]` only if the live call
  actually confirms the restriction, not merely because the pricing page says so.

## 7. Docs to update

- New `docs/articles/tts.md`, following the structure of `docs/articles/image-generation.md` (the
  closest existing analog: a media-generation feature built on `responseModalities`).
- Add it to `docs/articles/toc.yml` and `docs/index.md`'s guide list.
- `README.md`: one new bullet plus a short code sample, matching the existing image-generation
  section's format.
- `Junaid.GoogleGemini.Net.csproj`'s `PackageReleaseNotes`: new version entry.
- Written in this repo's `CLAUDE.md` style throughout: short sentences, no em dashes.

## 8. Correctness checklist (verify each before calling this done)

- [x] `SpeechConfig`/`VoiceConfig`/`PrebuiltVoiceConfig`/`MultiSpeakerVoiceConfig`/`SpeakerVoiceConfig`
      registered in the source-generated JSON context, same as every other serialized type. (Nested
      types are auto-discovered through `GenerationConfig`, already a registered root's descendant;
      no explicit entry was needed.)
- [x] `BuildSpeechConfig` returns `null` when neither `VoiceName` nor `SpeakerVoices` is set, so a
      plain-text request is unaffected. Covered by a unit test.
- [x] `SpeakerVoices` takes priority over `VoiceName` when both are set, and this is documented on
      both properties, not just in this plan. Covered by a unit test.
- [x] `GeneratedAudio.ToWav()` produces byte-correct output verified against a real decoded clip, not
      just a header that merely parses. Confirmed three ways: a live test that reads the actual PCM
      samples and requires real amplitude variance (not silence or a corrupt constant buffer, see
      `AssertHasRealAudioSignal` in `AudioGenerationLiveTests.cs`); the `/speak` endpoint added to the
      ASP.NET Core sample, actually run and hit with a real request; and the output file independently
      identified by the Unix `file` command as `RIFF ... WAVE audio, Microsoft PCM, 16 bit, mono
      24000 Hz`, an external tool with no knowledge of this library's own assumptions.
- [x] Cost governance prices `AUDIO`-modality output at the correct per-model rate. Pricing numbers
      still came from a single fetch of the pricing page, not a live billing check; that specific
      re-verification remains open (see §2).
- [x] `netstandard2.0` build target still compiles with any new syntax used. Confirmed via
      `dotnet build` on all three targets, and separately by actually decoding/writing a WAV file
      through the ASP.NET Core sample (net9.0, not netstandard2.0 itself, but exercising the same
      `ToWav()` code path the netstandard2.0 build also compiles).
- [x] Full solution build is 0 warnings, 0 errors; unit and live suites both green, before
      considering this done, per `RELEASE-RUNBOOK.md`. All three TTS model constants confirmed live,
      not just the first one tested (see the 2026-09-06 update in §2 for why this mattered).
- [x] No em dashes anywhere in new code comments, XML docs, or this plan (standing repo rule,
      `CLAUDE.md`).

## 9. Interactions API: evaluated and deferred

Google's docs describe a second way to call TTS: the Interactions API (`client.interactions.create()`,
`POST /v1beta/interactions`). This library does not use it. This section records why, so a future
maintainer does not have to re-derive the reasoning from scratch.

**What it is.** A newer API, GA since June 2026, meant to unify chat, tool calls, and agent turns
behind one `Interaction` resource. Its TTS page shows a different request shape entirely: `model`,
`input`, `response_format`, and `generation_config.speech_config` as an array of `{speaker, voice}`
objects. The response carries audio in `interaction.output_audio.data`, not
`candidates[].content.parts[].inlineData`. It is not a drop-in swap for the shape this library uses.

**Why this library still uses `generateContent`.** Three reasons, checked live on 2026-09-06:

1. Google's own docs call `generateContent` "Legacy" but state it "remains fully supported," with no
   deprecation date given. It is not being removed.
2. The Interactions API does not yet support the Batch API, a feature this library already ships and
   markets as a differentiator. Forking TTS onto Interactions now would leave two incompatible client
   shapes in this library, one for every other feature and one just for speech, for a label with no
   removal date behind it.
3. The Interactions API has no official .NET SDK to check a wire format against (Python and JS only
   as of this writing). Building on it now means reverse-engineering the shape from docs alone, on a
   surface still filling in gaps post-GA, the same risk that produced this file's `mimeType` bug, but
   on less stable ground.

**When to revisit.** Once the Interactions API reaches feature parity with `generateContent` (Batch
API support, in particular), or once Google sets an actual deprecation date for `generateContent`.
Tracked as a non-urgent backlog item in `ROADMAP.md`, not a same-release pivot.
