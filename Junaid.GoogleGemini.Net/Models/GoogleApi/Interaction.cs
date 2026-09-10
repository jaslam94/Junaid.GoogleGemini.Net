using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Junaid.GoogleGemini.Net.Exceptions;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// A response from the Interactions API (<c>POST /v1beta/interactions</c> or
/// <c>GET /v1beta/interactions/{id}</c>). Used today only for speech-to-text, see
/// <see cref="Services.Interfaces.ITranscriptionService"/>. This is not a <see cref="GenerateContentResponse"/>:
/// it is a different shape entirely, live-verified in <c>PLAN-stt.md</c>.
/// </summary>
public class Interaction
{
    /// <summary>The interaction's server-assigned ID. Pass to <c>ITranscriptionService.GetInteractionAsync</c> to poll it.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>e.g. <c>"completed"</c>, <c>"in_progress"</c>. Every call made while building this
    /// feature returned <c>"completed"</c> immediately (all test clips were a few seconds long); long
    /// audio's behavior was not confirmed, see <c>PLAN-stt.md</c> §7.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Token usage. Read <see cref="InteractionUsage.ModelInvocationTokenCounts"/> for the
    /// real output token count; see the warning on <see cref="InteractionUsage.TotalOutputTokens"/>.</summary>
    [JsonPropertyName("usage")]
    public InteractionUsage? Usage { get; set; }

    [JsonPropertyName("created")]
    public DateTimeOffset? Created { get; set; }

    [JsonPropertyName("updated")]
    public DateTimeOffset? Updated { get; set; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }

    /// <summary>
    /// The interaction's steps. A <c>GET</c> response includes a leading <c>"user_input"</c> step
    /// echoing what was sent; a <c>POST</c> response does not. Confirmed live, see <c>PLAN-stt.md</c>
    /// §3.2. Use <see cref="Text"/>/<see cref="Words"/> rather than reading this directly, unless you
    /// need the raw step structure.
    /// </summary>
    [JsonPropertyName("steps")]
    public List<InteractionStep>? Steps { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Gets the transcript text, concatenated across every <c>"model_output"</c> step (a
    /// <c>"user_input"</c> echo step, present only on a <c>GET</c> response, is skipped). Returns an
    /// empty string, never null, matching <see cref="GenerateContentResponse.Text"/>. Use
    /// <see cref="TryGetText"/> or <see cref="GetTextOrThrow"/> to distinguish "no text" from a
    /// genuinely empty transcript.
    /// </summary>
    public string Text() => GetTextInternal() ?? string.Empty;

    /// <summary>Gets the transcript text, returning <c>false</c> (and a null <paramref name="text"/>)
    /// when the interaction has no <c>"model_output"</c> content.</summary>
    public bool TryGetText([NotNullWhen(true)] out string? text)
    {
        text = GetTextInternal();
        return text is not null;
    }

    /// <summary>Gets the transcript text, or throws <see cref="GeminiContentException"/> when there is none.</summary>
    public string GetTextOrThrow()
    {
        return GetTextInternal()
            ?? throw new GeminiContentException("The interaction contained no transcript text.");
    }

    /// <summary>
    /// Flattens every <c>"word_info"</c> annotation across all <c>"model_output"</c> steps, in order.
    /// Each entry's <see cref="InteractionAnnotation.Speaker"/> is set only when the request enabled
    /// diarization (<see cref="TranscriptionConfig.DiarizationMode"/>); each entry's
    /// <see cref="InteractionAnnotation.StartOffset"/>/<see cref="InteractionAnnotation.EndOffset"/>
    /// are set only when the request enabled timestamps (<see cref="TranscriptionConfig.TimestampGranularities"/>).
    /// Empty (never null) when neither was requested, or the interaction has no output yet.
    /// </summary>
    public IReadOnlyList<InteractionAnnotation> Words()
    {
        if (Steps is not { Count: > 0 } steps) return [];

        return steps
            .Where(s => s.Type == "model_output")
            .SelectMany(s => s.Content ?? [])
            .SelectMany(c => c.Annotations ?? [])
            .Where(a => a.Type == "word_info")
            .ToList();
    }

    private string? GetTextInternal()
    {
        if (Steps is not { Count: > 0 } steps) return null;

        var joined = string.Concat(
            steps.Where(s => s.Type == "model_output")
                 .SelectMany(s => s.Content ?? [])
                 .Where(c => c.Text is not null)
                 .Select(c => c.Text));

        return string.IsNullOrEmpty(joined) ? null : joined;
    }
}

/// <summary>One step of an <see cref="Interaction"/>. <c>Type</c> is a real discriminator: a
/// <c>GET</c> response's first step can be <c>"user_input"</c> (echoing what was sent, no transcript
/// text), not just <c>"model_output"</c>. Confirmed live, see <c>PLAN-stt.md</c> §3.2.</summary>
public class InteractionStep
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("content")]
    public List<InteractionContent>? Content { get; set; }
}

/// <summary>One content item within an <see cref="InteractionStep"/>. <see cref="Uri"/>/<see cref="MimeType"/>
/// are populated only on a <c>"user_input"</c> step's echoed input, not on <c>"model_output"</c>.</summary>
public class InteractionContent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>The transcript text, on a <c>"model_output"</c> step.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Word-level annotations, present only when timestamps and/or diarization were requested.</summary>
    [JsonPropertyName("annotations")]
    public List<InteractionAnnotation>? Annotations { get; set; }

    /// <summary>Echoed input URI, present only on a <c>"user_input"</c> step from a <c>GET</c> response.</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    /// <summary>Echoed input MIME type, present only on a <c>"user_input"</c> step from a <c>GET</c> response.</summary>
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
}

/// <summary>
/// One word-level annotation. Live-verified shape, see <c>PLAN-stt.md</c> §3.2: character offsets
/// (<see cref="StartIndex"/>/<see cref="EndIndex"/>) are always present when annotations exist at
/// all; <see cref="StartOffset"/>/<see cref="EndOffset"/> (audio-time offsets, e.g. <c>"0.300s"</c>)
/// require <see cref="TranscriptionConfig.TimestampGranularities"/>; <see cref="Speaker"/> requires
/// <see cref="TranscriptionConfig.DiarizationMode"/>.
/// </summary>
public class InteractionAnnotation
{
    /// <summary>Always <c>"word_info"</c> today.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>The word's text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Character offset into the step's full <see cref="InteractionContent.Text"/>.</summary>
    [JsonPropertyName("start_index")]
    public int? StartIndex { get; set; }

    /// <summary>Character offset into the step's full <see cref="InteractionContent.Text"/>.</summary>
    [JsonPropertyName("end_index")]
    public int? EndIndex { get; set; }

    /// <summary>Audio-time offset, e.g. <c>"0.300s"</c>. A string, not a number: parse it yourself if
    /// you need a numeric value. Present only when timestamps were requested.</summary>
    [JsonPropertyName("start_offset")]
    public string? StartOffset { get; set; }

    /// <summary>Audio-time offset, e.g. <c>"0.400s"</c>. Present only when timestamps were requested.</summary>
    [JsonPropertyName("end_offset")]
    public string? EndOffset { get; set; }

    /// <summary>
    /// Zero-indexed speaker label, e.g. <c>"spk:0"</c>, <c>"spk:1"</c>. Confirmed live on a real
    /// two-speaker clip (<c>PLAN-stt.md</c> §3.2); this exact colon-separated, zero-indexed format is
    /// what the real API returns, not the underscore/one-indexed format some docs describe. Present
    /// only when diarization was requested.
    /// </summary>
    [JsonPropertyName("speaker")]
    public string? Speaker { get; set; }
}
