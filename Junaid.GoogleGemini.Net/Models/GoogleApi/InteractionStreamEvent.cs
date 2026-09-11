namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// One Server-Sent Event from a streamed <see cref="InteractionRequest"/> (<c>?alt=sse</c> with
/// <c>Stream = true</c>). A materially different SSE protocol than <c>streamGenerateContent</c>'s:
/// that one sends a full <see cref="GenerateContentResponse"/> on every <c>data:</c> line; this one
/// sends named events with different payload shapes per name. Live-verified, see <c>PLAN-stt.md</c>
/// §3.2. Named events observed: <c>interaction.created</c>, <c>interaction.status_update</c>,
/// <c>step.start</c>, <c>step.delta</c>, <c>step.stop</c>, <c>interaction.completed</c>, <c>done</c>.
/// </summary>
public class InteractionStreamEvent
{
    /// <summary>The SSE <c>event:</c> line's value, e.g. <c>"step.delta"</c>.</summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Incremental transcript text, populated only for a <c>step.delta</c> event whose
    /// <c>delta.type</c> is <c>"text"</c>. For the short clips this library was live-tested against,
    /// the entire transcript arrived as a single delta; whether a long clip arrives as multiple
    /// deltas was not confirmed, see <c>PLAN-stt.md</c> §7.
    /// </summary>
    public string? DeltaText { get; set; }

    /// <summary>
    /// The full interaction object, populated on <c>interaction.created</c> (status
    /// <c>"in_progress"</c>) and <c>interaction.completed</c> (status <c>"completed"</c>, carrying the
    /// final <see cref="Interaction.Usage"/>). Null on every other event type.
    /// </summary>
    public Interaction? Interaction { get; set; }
}
