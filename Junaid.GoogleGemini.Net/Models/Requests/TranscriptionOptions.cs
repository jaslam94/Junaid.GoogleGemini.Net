using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Models.Requests;

/// <summary>
/// Options for <see cref="Services.Interfaces.ITranscriptionService"/>. Not a
/// <see cref="GeminiRequestOptions"/>: transcription is called through the Interactions API, not
/// <c>generateContent</c>, so none of that type's sampling/safety/response-modality fields apply.
/// See <c>PLAN-stt.md</c> for the live verification each field below is built from.
/// </summary>
public class TranscriptionOptions
{
    /// <summary>The model to call. Defaults to <see cref="GeminiConstants.Models.RecommendedTranscription"/>.</summary>
    public string Model { get; set; } = GeminiConstants.Models.RecommendedTranscription;

    /// <summary>BCP-47 language hints, e.g. <c>["en-US"]</c>. Confirmed accepted by the real API;
    /// auto-detection applies when left unset.</summary>
    public IReadOnlyList<string>? LanguageCodes { get; set; }

    /// <summary>
    /// Domain-specific terms to bias recognition toward. Confirmed live: the API rejects this
    /// (HTTP 400, "custom_vocabulary is incompatible with diarization") when combined with
    /// <see cref="DiarizationMode"/> or <see cref="TimestampGranularities"/>. This is validated
    /// client-side in the constructors that use this type, before any network call.
    /// </summary>
    public IReadOnlyList<string>? CustomVocabulary { get; set; }

    /// <summary>
    /// Set to <c>"speaker"</c> for per-word speaker labels (see <see cref="GoogleApi.InteractionAnnotation.Speaker"/>).
    /// Confirmed incompatible with <see cref="CustomVocabulary"/>. Plain <c>string</c>, not an enum,
    /// matching this library's existing approach to API string values that Google can add to without
    /// a library update (see <c>GeminiConstants.Models</c>' own doc comment).
    /// </summary>
    public string? DiarizationMode { get; set; }

    /// <summary>e.g. <c>["word"]</c>. Confirmed incompatible with <see cref="CustomVocabulary"/>.</summary>
    public IReadOnlyList<string>? TimestampGranularities { get; set; }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <see cref="CustomVocabulary"/> is set together with
    /// <see cref="DiarizationMode"/> or <see cref="TimestampGranularities"/>: this is a real,
    /// live-confirmed HTTP 400 from the API (see <c>PLAN-stt.md</c> §3.4), so it is caught here
    /// instead of on a wasted network round-trip.
    /// </summary>
    public void Validate()
    {
        if (CustomVocabulary is { Count: > 0 } &&
            (DiarizationMode is not null || TimestampGranularities is { Count: > 0 }))
        {
            throw new ArgumentException(
                $"{nameof(CustomVocabulary)} cannot be combined with {nameof(DiarizationMode)} or " +
                $"{nameof(TimestampGranularities)}. The Gemini API rejects this combination " +
                "(confirmed: \"custom_vocabulary is incompatible with diarization.\"). Use one or the other.");
        }
    }
}
