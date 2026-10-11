# Plan: Gemini 3.8 TTS and the October 2026 model refresh (6.8.0)

Status: implemented in `6.8.0`, not yet released. All evidence below comes from real API calls on
2026-10-10.

## 1. Why this release exists

Google shuts down all three TTS models this library ships on **2026-11-17**:
`gemini-2.5-flash-preview-tts`, `gemini-2.5-pro-preview-tts` and `gemini-3.1-flash-tts-preview`.
`GenerateAudioAsync` uses the 2.5 Flash model by default. After that date, a plain call fails.

The replacements are `gemini-3.8-flash-tts` and `gemini-3.8-flash-lite-tts` (GA 2026-09-22).
They are not a drop-in swap. Four things changed.

Other dated changes also need action:
- Google's introductory price for 3.6 and 3.8 Flash, and for the 3.8 TTS models, ends on
  2026-12-31. The rate doubles on 2027-01-01.
- Google deprecated `gemini-3.5-flash` and `gemini-3.7-flash` on 2026-10-08. It routes them to
  3.6 and 3.8.
- Google released `gemini-nano-banana-2.1` on 2026-10-06 and deprecated `gemini-3.1-flash-image`.

## 2. What we verified live

| Question | Result |
|---|---|
| Do the 3.8 TTS models work through `generateContent`? | Yes, for both. |
| What does a unary call return by default? | `audio/wav` with a RIFF header. `ToWav()` throws on this today. |
| Can we ask for raw PCM? | Yes. `generationConfig.responseFormat.audio.mimeType = "AUDIO_L16"` returns `audio/l16; rate=24000; channels=1`. |
| What does streaming return? | `audio/l16; rate=24000; channels=1` chunks. |
| Is a style hint in the text still read as an instruction? | No. The model speaks it. We transcribed the audio for "Say cheerfully: Have a wonderful day." and got "Say cheerfully, have a wonderful day." Output tokens also went from 46 to 83. |
| Where does style go? | `speechMetadata.style` on the text part. Accepted. |
| Does the current multi-speaker request work? | No. HTTP 400: "Multi-speaker generation requests must specify speech_metadata.speaker for each text part." |
| What works for multi-speaker? | One part per turn, each with `speechMetadata.speaker`, and no `Name:` labels in the text. |
| Do the old models accept `speechMetadata`? | No. HTTP 400: "Speech metadata is not supported for this model." |
| How are custom voices selected? | `voiceConfig.voice` with a `voice_...` id. The `prebuiltVoiceConfig` path returns 400 for an id. |
| Is `speechConfig.languageCode` accepted? | Yes, on 3.8. |
| Does `responseFormat.audio.sampleRate` work? | No. We asked for 24000, 16000 and 8000 on a unary call. The WAV header and the PCM `mimeType` said 24000 every time. We do not ship an option for it. |
| Which `mimeType` does each format return? | `AUDIO_WAV`: `audio/wav`. `AUDIO_L16`: `audio/l16; rate=24000; channels=1`. `AUDIO_MULAW`: `audio/mulaw; rate=24000; channels=1`. `AUDIO_ALAW`: `audio/alaw; rate=24000; channels=1`. |
| Is the mu-law `rate=24000` label true? | Probably not. A mu-law clip was about 52 KB, where the same text as 24 kHz PCM was about 340 KB. That fits 8 kHz. We did not confirm it. |
| Do the old models still work through this library after the change? | Yes. We ran `gemini-2.5-flash-preview-tts` and `gemini-3.1-flash-tts-preview` with a multi-speaker script and the old preamble, through the real client. Both transcribed correctly. An in-text direction still acts as a direction on the old model. |
| Does the multi-speaker split work for a stream? | Yes. A live test streams a script and transcribes the result. |
| Does a real custom voice work? | Yes. We made one with voice design (`POST /v1beta/voices`, type `prompted`). Both 3.8 models spoke with it through `voiceConfig.voice`. `gemini-2.5-flash-preview-tts` rejected it with 400. The voice can be read, listed and deleted, and a deleted voice returns 404. Creating it used 662 tokens. |
| Does voice design work on `gemini-3.8-flash-lite-tts`? | No. HTTP 400: "does not support Prompted Voice Design". Google's page says both models do. Leave `voice.model` out and the voice works with either model. |
| Is there a rate limit to know about? | Yes. `gemini-3.8-flash-lite-tts` returned HTTP 429 after about 10 calls in a minute on our key (limit 10 per minute per model). |

Other live results used by this release:
- `gemini-3.5-flash` reports `modelVersion: gemini-3.6-flash`. `gemini-3.7-flash` reports 3.8.
- `thinkingLevel: minimal` on `gemini-3.8-flash` returns 400.
- `thinkingBudget: 0` returns 200 on 3.6 and 3.8 and usually produces no thought tokens. It is not a guarantee: 1 call in 8 on 3.8 still thought for 67 tokens, and 0 in 8 on 3.6. A first version of its live test asserted "always zero" and failed on a later run.
- A history that ends with a model turn returns 400.
- Nano Banana 2.1 accepts `imageConfig` with ratio `1:8` through `generateContent`.
- Nano Banana 2.1 rejects image size `512` with 400.
- A function response without an `id` still works on 3.8. The API does return call ids.
- `Part.mediaProcessing` works with two real clips of about 10 seconds. Unset and `STATIC` give the
  same result (671 prompt tokens). `AGENTIC` answers correctly. Its response has extra `toolCall`
  and `toolResponse` parts, and the video tokens move to `usageMetadata.toolUsePromptTokenCount`
  (712 and 806 in two runs), leaving `promptTokenCount` at 11. The cost governor ignored that field,
  so we added it to `UsageMetadata` and price it as input. On these short clips `AGENTIC` used
  slightly more tokens, not fewer.

## 3. Decisions

1. **Keep the public API stable.** `SpeakerVoices` stays. The same prompt text keeps working.
2. **Auto-split multi-speaker scripts.** When `SpeakerVoices` is set and the model supports speech
   metadata, the library splits the prompt at each `Name:` marker. It sends one part per turn with
   `speechMetadata.speaker`. It drops any text before the first marker, because 3.8 reads it aloud.
3. **Choose the shape by model.** The three legacy models keep the old shape. Every other model
   uses speech metadata. A new `UseSpeechMetadata` option overrides this choice.
4. **Add `SpeechStyle`.** It maps to `speechMetadata.style`. On a legacy model it throws a clear
   error, because the API would return 400.
5. **Keep `ToWav()` working.** It returns the bytes unchanged when the audio is already WAV.
6. **Add an `AudioFormat` option.** It maps to `responseFormat.audio.mimeType`. We planned a
   sample-rate option too and dropped it. Google documents `sampleRate`, but the API ignored
   requests for 16000 and 8000 in a live test (the WAV header and the PCM `mimeType` stayed at
   24000). We do not ship an option that does nothing.
7. **Add `SpeechLanguageCode`.** It maps to `speechConfig.languageCode`.
8. **Send custom voices correctly.** A `VoiceName` that starts with `voice_` or `voicekey_` goes in
   `voiceConfig.voice`. Other names stay in `prebuiltVoiceConfig`.
9. **New default TTS model: `gemini-3.8-flash-lite-tts`.** It is the cheaper model and has a free
   tier. It is also Google's named replacement for the 3.1 model.
10. **Make pricing date-aware.** `ModelPricing` gets an optional `NextPricing` and an effective
    date. The built-in table carries the 2027-01-01 rates. We treat the date as 00:00 UTC.
11. **Price redirected models at their real rate.** `gemini-3.5-flash` uses the 3.6 rate.
    `gemini-3.7-flash` uses the 3.8 rate.
12. **Keep `RecommendedImage` at `gemini-3.1-flash-image`.** We first planned to move it to
    `gemini-nano-banana-2.1`, because Google deprecated the 3.1 model. The live suite changed our
    mind. Nano Banana 2.1 returned `finishReason: IMAGE_RECITATION` and no image for "a small red
    circle on a plain white background" (2 of 2 calls) and for a plain blue rectangle (1 of 6
    calls, and the same prompt worked on the next call). The old model returned an image. Nano
    Banana 2.1 stays available as an opt-in constant. We move the default when Google sets a
    shutdown date for the old model.

## 4. Smaller changes

- New constants: both 3.8 TTS models, `gemini-nano-banana-2.1`, four wide and tall aspect ratios,
  the `512` image size, audio format values, and media processing values.
- `GenerateContentResponse` gets `ModelVersion`, so a caller can see which model answered.
- `Part` gets `SpeechMetadata` and `MediaProcessing`. `FunctionCallPart` and `FunctionResponsePart`
  get `Id`, so callers can echo it.
- Mark obsolete: `Gemini35Flash`, `Gemini37Flash`, and the three legacy TTS constants.
- Fix docs: thinking levels differ by model, and the sampling presets are ignored.
- Update the docs, the sample, the live tests, the release notes and the roadmap.

## 5. What this release does not do

- It does not add the Voices endpoint (list, design, replicate). That is a separate feature.
- It does not add the Live API. That stays a separate package.
- It does not move to the Interactions API. Google documents `generateContent` as legacy but fully
  supported, and the new models work through it. The Interactions API still lacks Batch, explicit
  caching and safety settings.
- It does not test the long-video savings of `Part.MediaProcessing`. We tested the field against two
  real clips of about 10 seconds (see section 2). It works, but Google's claim of up to 88% fewer
  tokens is for long videos, and we had none to test.
- It does not add the tool-use tokens to the OpenTelemetry token metrics. Those still report only
  `promptTokenCount`, so they under-report agentic video calls. The cost governor does count them.

## 6. Risks

- The marker split can misfire when dialogue contains `Name:` inside a sentence. The docs say so.
- Treating every non-legacy model as "supports speech metadata" is a guess for future models.
  `UseSpeechMetadata` is the escape hatch.
- The pricing date is 00:00 UTC. Google does not state a time zone.

## 7. Test plan

- Unit tests: request shape for every new option, the marker split, `ToWav()` on WAV, scheduled
  pricing before and after the date, constants, and `Id` round trips.
- Live tests against the real key: both 3.8 models, multi-speaker, streaming, audio format,
  Nano Banana 2.1 at `1:8`, and the default image call.
- `dotnet build -c Release` must show 0 warnings on all three targets.
