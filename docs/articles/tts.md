# Text-to-speech (TTS)

Gemini's TTS models return generated speech from the same `generateContent` endpoint used for text.
The client just needs to ask for `AUDIO` output. `GenerateAudioAsync` does that for you.

```csharp
var response = await gemini.GenerateAudioAsync("Say cheerfully: Have a wonderful day!");
var audio = response.GetAudioOrThrow();
await File.WriteAllBytesAsync("greeting.wav", audio.ToWav());
```

> **Note:** all three TTS models are `-preview` model IDs as of this writing. Google may change or
> replace them; pin a version you have tested if that matters for your deployment.

## Reading the result

Mirrors the `Text()`/`Images()` accessor trios on `GenerateContentResponse`, but singular: nothing
observed or documented suggests a TTS response ever returns more than one audio clip per call.

```csharp
GeneratedAudio? audio = response.Audio();              // null if none, never throws
bool found = response.TryGetAudio(out var maybeAudio);  // false + null when there is none
GeneratedAudio guaranteed = response.GetAudioOrThrow();  // throws GeminiContentException if none
```

## Making the audio playable

Gemini returns raw 16-bit linear PCM audio, not a `.wav` or `.mp3` file. Writing
`GeneratedAudio.Data` straight to disk produces a file most players cannot open. Call `ToWav()` to
get a real, playable WAV file instead:

```csharp
byte[] wavBytes = audio.ToWav(); // adds a correct 44-byte WAV header; parses the sample rate
                                  // from MimeType, so this works whichever TTS model you used
```

Two real response `mimeType` formats have been confirmed: `"audio/L16;codec=pcm;rate=24000"` from
the 2.5-era TTS models, and `"audio/l16; rate=24000; channels=1"` from
`gemini-3.1-flash-tts-preview`. `ToWav()` handles both.

`ToWav()` throws `FormatException` if a future model ever returns a different audio codec than every
Gemini TTS model does today.

## Choosing a voice

Set `VoiceName` for a single voice covering the whole input:

```csharp
var options = new GeminiRequestOptions { VoiceName = "Kore" };
var response = await gemini.GenerateAudioAsync("Welcome to the show!", options);
```

This library does not enumerate Google's 30 named voices (`Kore`, `Puck`, `Charon`, and so on) as
constants. Pass any current voice name as a plain string; see
[Google's TTS voice list](https://ai.google.dev/gemini-api/docs/generate-content/speech-generation)
for the current full set. A typo is the API's to reject, not this library's to catch ahead of time,
the same policy this library already applies to model names.

## Multi-speaker audio

Set `SpeakerVoices` for a script with more than one named speaker. Name each speaker in the prompt
text itself, then map each name to a voice:

```csharp
var options = new GeminiRequestOptions
{
    SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")],
};
var response = await gemini.GenerateAudioAsync(
    "TTS the following conversation between Joe and Jane: Joe: Hi Jane! Jane: Hello Joe!",
    options);
```

`SpeakerVoices` takes priority over `VoiceName` when both are set: a script naming multiple speakers
cannot sensibly also use one global voice.

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

A short clip may arrive as a single chunk. Only the final chunk is guaranteed to carry
`chunk.Usage`.

## Cost and usage

Audio output tokens are billed as regular output tokens, tagged `AUDIO` in
`UsageMetadata.CandidatesTokensDetails`, so cost governance (see
[Cost governance](cost-governance.md)) prices them automatically using the same per-model rate table
every other feature uses. No separate setup is needed.

> **Note:** pricing and free-tier availability differ per TTS model. Check
> [Google's current pricing page](https://ai.google.dev/gemini-api/docs/pricing) before choosing a
> model for a cost-sensitive or free-tier deployment.
