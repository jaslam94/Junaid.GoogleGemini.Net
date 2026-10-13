# Plan: Text-to-speech (TTS) for Junaid.GoogleGemini.Net

Status: implemented and shipped in `6.5.0`. We verified it live against the real API on 2026-09-04
(design) and on 2026-09-06 (all three model constants, after implementation). On 2026-09-06 we also
verified it again against a fresh billing-enabled key (all 5 live tests plus the full 41-test live
suite). We also evaluated a newer "Interactions API" surface that Google now documents alongside
`generateContent`, and deliberately deferred it. See §9.

Update, October 2026: Google shuts down all three models this plan covers on 2026-11-17. The
replacement models change the request shape. `PLAN-tts-3-8.md` records what we found and what
`6.8.0` changed. Read it before you rely on §2 below.

## 1. Goal

Let a caller turn text into spoken audio with Gemini's native TTS models, with the same low friction
as `GenerateImageAsync`. There are two modes: single speaker (one voice for the whole text) and
multi speaker (a script with named speakers, each with a different voice).

## 2. What we verified live, and how

We confirmed everything below with real REST calls against `gemini-2.5-flash-preview-tts`. We did
not rely on docs alone. This matters. A first web search described a different, newer "Interactions
API" shape (`input`, `response_format`, `interaction.output_audio.data`). That shape does not match
this library's `generateContent`-based architecture at all. It was wrong for this feature, and we
discarded it before we wrote any code.

Real model IDs:
- `gemini-2.5-flash-preview-tts`
- `gemini-2.5-pro-preview-tts`
- `gemini-3.1-flash-tts-preview`

All three are `-preview` models today. We live-tested only `gemini-2.5-flash-preview-tts` directly.
The other two share the same documented request and response shape. We added them on that basis, and
we did not confirm them independently. If that matters for a release, re-check before shipping.

Real request shape, single speaker:
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

Real request shape, multi speaker:
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

Real response shape, `candidates[0].content.parts[0].inlineData`:
```json
{
  "mimeType": "audio/L16;codec=pcm;rate=24000",
  "data": "<base64 PCM bytes>"
}
```

We read the exact `mimeType` string, `audio/L16;codec=pcm;rate=24000`, directly from a live
response. The audio is raw 16-bit linear PCM at 24000 Hz. It is not a WAV or MP3 file. A caller who
writes `Data` straight to a `.wav` file gets an unplayable file. The caller must add a real WAV
header first (see §4.3).

Update, 2026-09-06, after implementation: the assumption two paragraphs above was half right. We
had assumed that the other two models "share the same documented request and response shape." The
request shape is shared. We confirmed that by calling all three models through the real
`GenerateAudioAsync`. The response `mimeType` is not shared. `gemini-2.5-flash-preview-tts` and
`gemini-2.5-pro-preview-tts` both return `audio/L16;codec=pcm;rate=24000` (capital L, no spaces,
explicit `codec=pcm`). `gemini-3.1-flash-tts-preview` returns `audio/l16; rate=24000; channels=1`
instead. That string is lowercase and spaced. It has no `codec=pcm` segment, and it has an explicit
channel count in its place. We had built the first version of `GeneratedAudio.ToWav()` from only the
first model we tested. It threw `FormatException` on this real response the first time we called
`gemini-3.1-flash-tts-preview`. We fixed it to parse the sample rate and the optional channel count
independently, wherever they appear in the string. It no longer matches one rigid whole-string
shape. This is the gap that the note "the other two share the same shape, not independently
confirmed" flagged in the first place. It took an actual call, and not a docs read, to find exactly
where the assumption broke.

We took the mono channel count and the 16-bit sample width from Google's own TTS guide prose. We did
not verify them live, because the response has no explicit channel-count field to check.

Real usage accounting: audio output tokens surface through the existing
`UsageMetadata.CandidatesTokensDetails` modality breakdown (added in `6.4.1`). They do not use a new
field:
```json
"usageMetadata": {
  "promptTokenCount": 8,
  "candidatesTokenCount": 51,
  "promptTokensDetails": [{"modality": "TEXT", "tokenCount": 8}],
  "candidatesTokensDetails": [{"modality": "AUDIO", "tokenCount": 51}]
}
```
So cost governance needs no new plumbing to see audio token counts. It needs only pricing entries
for the TTS models.

Streaming works. `streamGenerateContent?alt=sse` against the same model returned `HTTP 200`. The
streamed chunk had the same `inlineData` shape, and a final chunk carried `usageMetadata`. This
matches every other streaming response that this library already parses. Streaming needs no special
handling beyond registering the TTS models. The existing `StreamAsync` path applies as is. For the
short test text, the whole clip came back in a single SSE chunk. A longer script may arrive in more
than one chunk. The existing `StreamCoreAsync` chunk-accumulation logic already handles that
correctly for any part type.

We did not confirm these items independently. They come from Google's docs and pricing page (verify
before merging):
- Pricing: `gemini-2.5-flash-preview-tts` costs $0.50 in and $10.00 out per 1M tokens, and has a
  free tier. `gemini-2.5-pro-preview-tts` costs $1.00 in and $20.00 out, on the paid tier only.
  `gemini-3.1-flash-tts-preview` costs $1.00 in and $20.00 out, and has a free tier. These numbers
  came from a single fetch of the pricing page and not from a live billing check. Re-verify them
  before merging, as for every other pricing entry in `GeminiCostGovernor.DefaultPricing`.
- 30 named voices (Kore, Puck, Charon, Zephyr, and 26 more). The full list lives in Google's docs.
  We do not duplicate it as constants here (see §5 for why).
- A 32k token context window, and a caveat that quality can drift on clips longer than a few
  minutes.

## 3. Explicit scope

In scope (v1):
- Single-speaker generation: one voice for the whole input text.
- Multi-speaker generation: a script with named speakers, each mapped to a voice.
- A new `GeneratedAudio` response type with a `ToWav()` helper that adds a correct WAV header. The
  raw PCM that Google returns is not directly playable.
- Non-streaming (`GenerateAudioAsync`) and streaming (`StreamAudioAsync`), mirroring the split
  between `GenerateImageAsync` and the existing `StreamAsync`.
- Cost governance pricing entries for all three TTS models, using the numbers in §2, re-verified
  before merging.

Out of scope for this change (do not implement):
- The 30 voice names as C# constants. This library does not validate model names against an
  allow-list, so that new models work without a library update (see the doc comment of
  `GeminiConstants.Models`). Voice names get the same treatment: a plain `string`, not an enum.
  Google can add to or rename the 30 names. Hardcoding them invites the staleness that this library
  avoids elsewhere.
- Style control through inline tags (`[whispers]`, `[excited]`) or structured "Director's Notes"
  prompts. A caller can already use these plain-text conventions today by writing them into the
  prompt string. There is no separate API surface to wrap.
- The Live API's bidirectional audio. It is separate from this feature, and `ROADMAP.md` already
  tracks it as its own unstarted item.
- Any retry logic for the "occasional random text token" issue that Google's docs mention. That is a
  model-quality caveat to document. It is not a client-side workaround to build.

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

### 4.2 `GeminiRequestOptions` additions, mirroring `ImageAspectRatio` and `ImageSize`

```csharp
/// <summary>Voice for single-speaker TTS (see Google's TTS voice list). Gemini TTS models only.</summary>
public string? VoiceName { get; set; }

/// <summary>Speaker-to-voice map for multi-speaker TTS. Gemini TTS models only. When set, this
/// takes priority over <see cref="VoiceName"/>.</summary>
public IReadOnlyList<(string Speaker, string VoiceName)>? SpeakerVoices { get; set; }
```

`RequestFactory.CreateGenerationConfig` gets a new private method, `BuildSpeechConfig(options)`. It
is called and assigned in the same way as `BuildImageConfig`. It returns `null` when neither field
is set. It returns a single-speaker `SpeechConfig` when only `VoiceName` is set. It returns a
multi-speaker one when `SpeakerVoices` is set. `SpeakerVoices` wins if both are set, because a
script that names multiple speakers cannot sensibly also use one global voice.

Add both new properties to `GeminiRequestOptions.Clone()`, like every other property already there.

### 4.3 `GeneratedAudio` response type and `GenerateContentResponse` accessors

This mirrors `GeneratedImage`, `Images()`, `TryGetImages()`, and `GetImagesOrThrow()` exactly. It
adds one thing that the image types do not need: a way to make the raw PCM playable.

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
`GetAudioOrThrow()`. They filter `inlineData` parts to a `MimeType` that starts with `"audio/"`,
which mirrors the `"image/"` filter of `Images()` exactly. A TTS response has exactly one audio part
per call (nobody has observed or documented multi-clip responses). So these methods return a single
`GeneratedAudio?` and not a list, unlike the plural `Images()`.

### 4.4 `IGeminiService` methods

```csharp
Task<GenerateContentResponse> GenerateAudioAsync(
    string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default);

IAsyncEnumerable<GenerateContentResponse> StreamAudioAsync(
    string prompt, GeminiRequestOptions? options = null, CancellationToken cancellationToken = default);
```

Both are thin wrappers, exactly like `GenerateImageAsync`. On a cloned options object, they fill in
`Model ??= Models.RecommendedTts` and `ResponseModalities ??= [GeminiConstants.ResponseModalities.Audio]`.
Then they delegate to the existing internal generate or stream path. They pass a distinct operation
label ("audio generation") for accurate logs and errors. The doc comment on `GenerateInternalAsync`
already explains the reasoning.

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
No new code path is needed. `RecordSpend` and `ComputeCost` already price `CandidatesTokenCount` at
the model's `OutputPerMillionTokensUsd` rate. Audio tokens flow through that unchanged. Google
already counts them as regular `candidatesTokenCount` (confirmed in §2), with an `AUDIO` modality
tag on the breakdown. In the comment around the pricing table, add a note that matches the existing
note on image models' text and image output rates. The note should say that the table prices 100%
of output at the audio rate. This is correct for TTS. A TTS response has no separate text output
that the table could under-count or over-count, unlike the image models.

### 4.7 Telemetry

No new code is needed. `GeminiTelemetry.RecordUsage` and `RecordDuration` already instrument every
`PostAsync` and `StreamAsync` call by operation name and model. `GenerateAudioAsync` and
`StreamAudioAsync` inherit this through the shared internal path, as `GenerateImageAsync` already
does.

## 5. Naming and design decisions worth recording

- No voice-name constants. See §3. A caller passes a plain string for `VoiceName`. The API rejects
  a typo. The library does not try to prevent it.
- A tuple list for `SpeakerVoices`, not a dictionary. A `Dictionary<string, string>` would work for
  the common case. But it silently drops order. It also cannot represent the same speaker appearing
  twice with different config in some hypothetical future API shape. A `List<(string, string)>`
  costs nothing extra and keeps input order. Google's own multi-speaker example preserves the order
  too.
- `ToWav()` lives on `GeneratedAudio` and is not a free function. This keeps the path from decode
  to playable file discoverable from the type itself. It matches how the `Data` of `GeneratedImage`
  is already directly usable without a separate helper class to find.
- Singular `Audio()` and plural `Images()`. This is deliberate and not an inconsistency. Gemini's
  image models can return multiple images per call. Nothing observed or documented suggests that TTS
  ever returns more than one audio clip per call.

## 6. Tests to add

Unit tests (`tests/Junaid.GoogleGemini.Net.Tests`), based on `FakeHttpMessageHandler`:
- `GenerateAudioAsync` sends `responseModalities: ["AUDIO"]` and the correct single-speaker
  `speechConfig` shape when `VoiceName` is set.
- `GenerateAudioAsync` sends the correct multi-speaker `speechConfig` shape when `SpeakerVoices` is
  set. `SpeakerVoices` wins when both `VoiceName` and `SpeakerVoices` are set.
- `GenerateContentResponse.Audio()`, `TryGetAudio()`, and `GetAudioOrThrow()` decode a fake
  `inlineData` part correctly. They return `null` or throw as appropriate when no audio part is
  present.
- `GeneratedAudio.ToWav()` produces a byte array with a valid 44-byte RIFF/WAVE header for a known
  `mimeType` and `Data` pair. It throws `FormatException` for a `mimeType` that does not match the
  expected shape.

Live tests (`tests/Junaid.GoogleGemini.Net.IntegrationTests`), `[RequiresGeminiKey]`:
- Single-speaker `GenerateAudioAsync` against a real key returns audio that decodes. The `ToWav()`
  output starts with a valid `RIFF`/`WAVE` header.
- Multi-speaker `GenerateAudioAsync` against a real key succeeds (same shape check).
- `StreamAudioAsync` against a real key yields at least one chunk that carries an audio part. The
  final chunk carries `UsageMetadata` with an `AUDIO` modality entry.
- Confirm live whether `gemini-2.5-pro-preview-tts` requires a paid-tier key (per the
  pricing-page claim in §2). Gate that one test with `[RequiresPaidGeminiKey]` only if the live call
  confirms the restriction. Do not gate it only because the pricing page says so.

## 7. Docs to update

- Add `docs/articles/tts.md`, following the structure of `docs/articles/image-generation.md`. That is
  the closest existing analog: a media-generation feature built on `responseModalities`.
- Add it to `docs/articles/toc.yml` and to the guide list in `docs/index.md`.
- `README.md`: add one new bullet and a short code sample, matching the format of the existing
  image-generation section.
- `Junaid.GoogleGemini.Net.csproj`: add a new version entry to `PackageReleaseNotes`.
- Write everything in the style of this repo's `CLAUDE.md`: short sentences, no em dashes.

## 8. Correctness checklist (verify each item before you call this done)

- [x] `SpeechConfig`, `VoiceConfig`, `PrebuiltVoiceConfig`, `MultiSpeakerVoiceConfig`, and
      `SpeakerVoiceConfig` are registered in the source-generated JSON context, like every other
      serialized type. (The generator discovers nested types through `GenerationConfig`, which is
      already a registered root. No explicit entry was needed.)
- [x] `BuildSpeechConfig` returns `null` when neither `VoiceName` nor `SpeakerVoices` is set, so a
      plain-text request is unaffected. A unit test covers it.
- [x] `SpeakerVoices` takes priority over `VoiceName` when both are set. We document this on both
      properties and in this plan. A unit test covers it.
- [x] `GeneratedAudio.ToWav()` produces byte-correct output, verified against a real decoded clip.
      A header that merely parses is not enough. We confirmed it three ways. First, a live test
      reads the actual PCM samples and requires real amplitude variance, so silence or a corrupt
      constant buffer fails (see `AssertHasRealAudioSignal` in `AudioGenerationLiveTests.cs`).
      Second, we ran the `/speak` endpoint that we added to the ASP.NET Core sample and called it
      with a real request. Third, the Unix `file` command independently identified the output file
      as `RIFF ... WAVE audio, Microsoft PCM, 16 bit, mono 24000 Hz`. That is an external tool with
      no knowledge of this library's own assumptions.
- [x] Cost governance prices `AUDIO`-modality output at the correct per-model rate. The pricing
      numbers still came from a single fetch of the pricing page and not from a live billing check.
      That re-verification remains open (see §2).
- [x] The `netstandard2.0` build target still compiles with any new syntax we used. We confirmed it
      with `dotnet build` on all three targets. We also decoded and wrote a WAV file through the
      ASP.NET Core sample. That sample runs on net9.0 and not netstandard2.0, but it exercises the
      same `ToWav()` code path that the netstandard2.0 build also compiles.
- [x] The full solution build has 0 warnings and 0 errors, and the unit and live suites are both
      green, before we consider this done, per `RELEASE-RUNBOOK.md`. We confirmed all three TTS
      model constants live. The first one we tested was not the only one (see the 2026-09-06 update in §2
      for why this mattered).
- [x] No em dashes anywhere in new code comments, XML docs, or this plan (a standing repo rule, see
      `CLAUDE.md`).

## 9. Interactions API: evaluated and deferred

Google's docs describe a second way to call TTS: the Interactions API
(`client.interactions.create()`, `POST /v1beta/interactions`). This library does not use it. This
section records why, so a future maintainer does not have to derive the reasoning again.

What it is: a newer API, GA since June 2026. It aims to unify chat, tool calls, and agent turns
behind one `Interaction` resource. Its TTS page shows a different request shape entirely: `model`,
`input`, `response_format`, and `generation_config.speech_config` as an array of `{speaker, voice}`
objects. The response carries audio in `interaction.output_audio.data` and not in
`candidates[].content.parts[].inlineData`. It is not a drop-in swap for the shape that this library
uses.

Why this library still uses `generateContent`: three reasons, checked live on 2026-09-06.

1. Google's own docs call `generateContent` "Legacy" but state that it "remains fully supported,"
   with no deprecation date. Google is not removing it.
2. The Interactions API does not yet support the Batch API. This library already ships the Batch API
   and markets it as a differentiator. Moving TTS onto Interactions now would leave two incompatible
   client shapes in this library, one for every other feature and one just for speech. The reason
   would be a label with no removal date behind it.
3. The Interactions API has no official .NET SDK to check a wire format against (Python and JS only,
   as of this writing). Building on it now means reverse-engineering the shape from docs alone, on a
   surface that is still filling in gaps after GA. The `mimeType` bug in this file came from the same
   kind of risk, but this would be on less stable ground.

When to revisit: when the Interactions API reaches feature parity with `generateContent` (Batch API
support, in particular), or when Google sets an actual deprecation date for `generateContent`.
`ROADMAP.md` tracks this as a non-urgent backlog item. It is not a same-release pivot.
