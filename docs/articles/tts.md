# Text-to-speech (TTS)

Gemini's TTS models return generated speech from the same `generateContent` endpoint used for text.
The client just needs to ask for `AUDIO` output. `GenerateAudioAsync` does that for you.

```csharp
var response = await gemini.GenerateAudioAsync("Have a wonderful day!");
var audio = response.GetAudioOrThrow();
await File.WriteAllBytesAsync("greeting.wav", audio.ToWav());
```

## Models

| Constant | Model | Use it for |
|---|---|---|
| `Models.Gemini38FlashLiteTts` | `gemini-3.8-flash-lite-tts` | The default. Fast and cheap. Good for most speech. |
| `Models.Gemini38FlashTts` | `gemini-3.8-flash-tts` | The best voice quality. Audiobooks, studio narration, acting. |

Both models are GA and have a free tier. Both read the input as text only, up to 8,192 tokens.

Google shuts down the three older TTS models on November 17, 2026: `gemini-2.5-flash-preview-tts`,
`gemini-2.5-pro-preview-tts` and `gemini-3.1-flash-tts-preview`. The library marks their constants
obsolete. Move to a 3.8 model before that date.

> **Note:** Google's docs also describe a newer "Interactions API", with a different request shape.
> This library uses `generateContent`, the same request shape every other feature here uses. Google
> states `generateContent` "remains fully supported". The 3.8 TTS models work through it. The
> Interactions API does not yet support the Batch API, which this library ships. See `PLAN-tts.md`
> and `PLAN-tts-3-8.md` in the repo for the reasoning.

## What changed in 6.8.0

The 3.8 models behave differently from the older models. Four changes matter.

1. The prompt is a verbatim transcript. The model speaks every word you send. A direction such as
   `"Say cheerfully: Have a wonderful day!"` used to steer the voice. On a 3.8 model, the model reads
   "Say cheerfully" aloud. Use `SpeechStyle` for directions (see below).
2. A unary call returns a WAV file. The older models returned raw PCM. `ToWav()` now returns a WAV
   file unchanged, so the same code works with both.
3. Multi-speaker needs one part per turn. Google rejects the old `Name: line` labels inside one text
   with HTTP 400. The library splits your script for you (see below). Your code does not change.
4. The default model changed from `gemini-2.5-flash-preview-tts` to `gemini-3.8-flash-lite-tts`.

## Reading the result

The accessors mirror the `Text()` and `Images()` trios on `GenerateContentResponse`, but they are
singular. Nothing observed or documented suggests that a TTS response returns more than one audio
clip per call.

```csharp
GeneratedAudio? audio = response.Audio();              // null if none, never throws
bool found = response.TryGetAudio(out var maybeAudio);  // false + null when there is none
GeneratedAudio guaranteed = response.GetAudioOrThrow();  // throws GeminiContentException if none
```

## Making the audio playable

Call `ToWav()` to get a playable WAV file. It works for every format that the TTS models return
today:

```csharp
byte[] wavBytes = audio.ToWav();
```

We have confirmed three response `mimeType` values against the real API:

| `mimeType` | From | `ToWav()` does |
|---|---|---|
| `audio/wav` | A unary call to a 3.8 model | Returns the bytes unchanged. They already are a WAV file. |
| `audio/l16; rate=24000; channels=1` | A stream from a 3.8 model, or a request for `AUDIO_L16`. Also `gemini-3.1-flash-tts-preview`. | Adds a 44-byte WAV header. |
| `audio/L16;codec=pcm;rate=24000` | The 2.5 models | Adds a 44-byte WAV header. |

`ToWav()` throws `FormatException` for any other format, such as mu-law or A-law. It cannot convert
those.

### Choosing the audio format

Leave `AudioFormat` unset to get the model default. Set it when you need a specific encoding:

```csharp
var options = new GeminiRequestOptions
{
    AudioFormat = GeminiConstants.AudioFormats.Pcm16,   // raw 16-bit PCM instead of a WAV file
};
```

The values are `Pcm16`, `Wav`, `Mulaw` and `Alaw`. Use `Mulaw` or `Alaw` for telephony. Write those
bytes to your phone system yourself. They are not WAV data, and `ToWav()` refuses them.

Check the rate of a mu-law or A-law clip before you send it anywhere. In our test the `mimeType` said
`rate=24000`, but the clip was far too small for 24 kHz audio. We think it is 8 kHz.

The library has no sample-rate option. Google documents one, but in our test on 2026-10-10 the API
ignored requests for 16000 and 8000. The WAV header and the PCM `mimeType` stayed at 24000.

## Choosing a voice

Set `VoiceName` for a single voice that covers the whole input:

```csharp
var options = new GeminiRequestOptions { VoiceName = "Kore" };
var response = await gemini.GenerateAudioAsync("Welcome to the show!", options);
```

This library does not list Google's 30 named voices (`Kore`, `Puck`, `Charon`, and so on) as
constants. Pass any current voice name as a plain string. See
[Google's TTS voice list](https://ai.google.dev/gemini-api/docs/generate-content/speech-generation)
for the current full set. The API rejects a mistyped voice name. The library does not check it, which
matches how it treats model names.

### Custom voices

A custom voice is a voice that you create yourself. Google gives it an id that starts with `voice_`
(a stored voice) or `voicekey_` (a voice that is not stored). Pass the id as `VoiceName`, like any
other voice name:

```csharp
var options = new GeminiRequestOptions { VoiceName = "voice_YOUR_VOICE_ID" };
var response = await gemini.GenerateAudioAsync("Hello, this is my custom voice.", options);
```

The library sends ids that start with `voice_` or `voicekey_` in the field that Google uses for
custom voices. Both 3.8 models accept them. The older TTS models reject them.

You create a custom voice through Google's Voices API, and `IVoiceService` wraps it. There are two
ways to do it:

- Voice design. Describe the voice in words. Google creates it and returns the id. You can also try
  this without code in [Google AI Studio](https://aistudio.google.com/generate-speech), then copy the
  id.
- Voice replication. Give Google two recordings of a real speaker, with a consent check, to copy
  their voice. See the warning below.

To design a voice from code:

```csharp
var voice = await voices.CreateDesignedAsync(new DesignedVoiceRequest
{
    Description = "A calm, friendly narrator in her thirties with a clear American accent.",
    DisplayName = "Calm narrator",
    Gender = "female",
    LanguageCode = "en-US",
});

string id = voice.Id!;                       // "voice_..."
File.WriteAllBytes("preview.wav", voice.SampleAudio!.GetBytes());   // a short preview clip

var response = await gemini.GenerateAudioAsync("Hello!", new GeminiRequestOptions { VoiceName = id });
```

`AddGemini` registers `IVoiceService`. It also lists, gets, and deletes voices:

```csharp
// Browse Google's voice library. Filters match exactly, and two values for one filter mean "either".
var british = await voices.ListAsync(new VoiceListOptions { LanguageCodes = ["en-GB"], Genders = ["female"] });
await foreach (var v in voices.ListAllAsync(new VoiceListOptions { Types = ["prompted", "replicated"] }))
    Console.WriteLine($"{v.Id}  {v.DisplayName}  expires {v.ExpiresAt:d}");   // your own stored voices

var mine = await voices.GetAsync("voice_...");   // includes the preview clip
await voices.DeleteAsync("voice_...");
```

We tried it against the real API on 2026-10-10 and found these things:

- Filter values must match exactly. `Personas = ["Narrator"]` finds nothing, because the real persona
  is "Storyteller & Narrator". Read the exact values from a list result first. Google rejects an
  unknown filter name with HTTP 400.
- Get and delete work only for your own stored voices. A prebuilt voice, such as `achernar`, returns
  HTTP 404 for both. Find prebuilt voices with `ListAsync`.
- A list returns your own voices first, newest first, and then the prebuilt voices that match. With no
  filter that is more than a thousand voices, so filter first.
- Do not set `DesignedVoiceRequest.Model`. Google's page says both 3.8 models support voice design.
  The API answered HTTP 400 for `gemini-3.8-flash-lite-tts`. Leave it null, and the voice works with
  either TTS model.
- Creating a voice costs very little. Our test used 662 tokens, mostly for the preview clip. Google
  lists no separate price. That usage does not feed cost governance, and it counts against your
  spending cap.
- Stored voices have limits. A project can keep 200 of them, and each one expires after a year.

Voice replication copies a real person's voice. Only use recordings of a speaker who has agreed.
Google requires two recordings of the same adult: 10 to 30 seconds of clean speech, and the speaker
reading Google's consent statement. `CreateReplicatedAsync` takes both. The library does not check the
consent and does not make one for you. We have not run this method against the real API, because a
test needs a real person's voice and consent. It follows Google's documented request shape. The
library does not support the stateless `voicekey_` variant. To test it yourself, record your own
voice, then set `GeminiTestVoiceSource` and `GeminiTestVoiceConsent` to the two files and run
`VoiceServiceLiveTests`.

The live tests in the repo cover the rest. `CustomVoiceLiveTests` and `VoiceServiceLiveTests` create a
voice, use it with both models, check the speech with a transcript, and delete it.

Set `SpeechLanguageCode` (for example `"en-US"`) to name the language. It is optional. The 3.8 models
detect the language from the text.

## Directing the delivery

Set `SpeechStyle` to a short direction. The library sends it as speech metadata. The model does not
read it aloud.

```csharp
var options = new GeminiRequestOptions { VoiceName = "Kore", SpeechStyle = "cheerful and upbeat" };
var response = await gemini.GenerateAudioAsync("Have a wonderful day!", options);
```

Google's docs also describe angle-bracket tags for short sounds inside the text, such as `<laugh>`,
`<sigh>`, `<breath>` and `<short pause>`. This library passes them through unchanged. We have not
tested them ourselves.

`SpeechStyle` works only on the 3.8 models. With an older model it throws `InvalidOperationException`,
because the API would reject it. For those models, write the direction in the prompt text.

## Multi-speaker audio

Set `SpeakerVoices` for a script with more than one named speaker. Write each turn as `Name: text`,
then map each name to a voice:

```csharp
var options = new GeminiRequestOptions
{
    SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")],
};
var response = await gemini.GenerateAudioAsync("Joe: Hi Jane! Jane: Hello Joe!", options);
```

On a 3.8 model the library splits the script at each `Name:` marker. It sends one part per turn, each
with the speaker's name in speech metadata. Keep these rules in mind:

- A marker is a name from `SpeakerVoices`, spelled exactly the same including case, followed by a
  colon. It may not follow a letter or digit, so `Joey:` does not match `Joe`.
- The library drops any text before the first marker. The 3.8 models would read it aloud. The old
  preamble `"TTS the following conversation between Joe and Jane: ..."` still works, because the
  library drops it.
- A speaker name followed by a colon in the middle of a sentence also starts a new turn. Rephrase such
  a line.
- If the prompt has no marker at all, the call throws `ArgumentException` before it sends anything.
- Google allows two speakers in one request, and only named (prebuilt) voices. For custom voices,
  make one call for each turn and join the audio yourself.
- `SpeechStyle` applies to every turn.

The older models still get the prompt exactly as you wrote it. The library picks the shape from the
model name. Set `UseSpeechMetadata` to `true` or `false` to override that choice, for example for a
TTS model that Google releases after this library version.

`SpeakerVoices` takes priority over `VoiceName` when you set both. A script that names multiple
speakers cannot sensibly also use one global voice.

## Streaming

`StreamAudioAsync` mirrors `StreamAsync`, yielding each response chunk as it arrives:

```csharp
await foreach (var chunk in gemini.StreamAudioAsync("A long announcement..."))
{
    if (chunk.Audio() is { } clip)
    {
        // handle each chunk's audio as it arrives
    }
}
```

A 3.8 model streams raw PCM (`audio/l16; rate=24000; channels=1`). To save the whole clip, join the
bytes of every chunk, then wrap the result once:

```csharp
var pcm = chunks.Select(c => c.Audio()).Where(a => a is not null).SelectMany(a => a!.Data).ToArray();
byte[] wav = new GeneratedAudio("audio/l16; rate=24000; channels=1", pcm).ToWav();
```

If you call `ToWav()` on each chunk, you put a WAV header in front of every chunk.

A short clip may arrive as a single chunk. Only the final chunk is guaranteed to carry `chunk.Usage`.

## Cost and usage

Audio output tokens are billed as regular output tokens. They are tagged `AUDIO` in
`UsageMetadata.CandidatesTokensDetails`. Cost governance (see [Cost governance](cost-governance.md))
prices them automatically, using the same per-model rate table as every other feature. No separate
setup is needed.

Google charges an introductory price for the 3.8 TTS models through December 31, 2026. The price
doubles on January 1, 2027. The built-in rate table already knows both prices and switches on that
date.

> **Note:** pricing and free-tier availability differ per TTS model. Check
> [Google's current pricing page](https://ai.google.dev/gemini-api/docs/pricing) before choosing a
> model for a cost-sensitive or free-tier deployment.
