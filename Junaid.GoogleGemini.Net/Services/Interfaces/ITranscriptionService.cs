using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces;

/// <summary>
/// Speech-to-text via Gemini's dedicated transcription model
/// (<see cref="Infrastructure.Utilities.GeminiConstants.Models.Gemini35Transcribe"/>). A separate
/// interface from <see cref="IGeminiService"/>, not a method on it: this is called through the
/// Interactions API, not <c>generateContent</c>, and returns <see cref="Interaction"/>, not
/// <see cref="GenerateContentResponse"/>. Folding it into <see cref="IGeminiService"/> (whose own doc
/// comment calls it "the unified service interface for all Gemini content generation operations",
/// every method returning/streaming <see cref="GenerateContentResponse"/>) would break that contract
/// for no benefit. See <c>PLAN-stt.md</c> §5.4/§6 for the full reasoning, and §2 for why this feature
/// needed the Interactions API at all: it is the only way to get diarization and word timestamps, the
/// actual differentiators of the dedicated transcription model.
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Transcribes inline audio bytes. For large files, prefer <see cref="TranscribeFileAsync"/> via
    /// <see cref="IFileService.UploadFileAsync"/>: the inline size limit for this specific endpoint
    /// was not confirmed live (do not assume <c>generateContent</c>'s informal ~20MB guidance
    /// applies here), so a large payload may fail unpredictably if sent inline.
    /// </summary>
    /// <param name="audioBytes">The raw audio bytes.</param>
    /// <param name="mimeType">The audio's MIME type, e.g. <c>"audio/wav"</c>.</param>
    /// <param name="options">Transcription options. Validated before the call; see <see cref="TranscriptionOptions.Validate"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Interaction> TranscribeAsync(
        byte[] audioBytes,
        string mimeType,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Transcribes audio already uploaded via <see cref="IFileService.UploadFileAsync"/>.</summary>
    /// <param name="fileUri">The uploaded file's URI (<see cref="FileResource.Uri"/>).</param>
    /// <param name="mimeType">The audio's MIME type, e.g. <c>"audio/wav"</c>.</param>
    /// <param name="options">Transcription options. Validated before the call; see <see cref="TranscriptionOptions.Validate"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Interaction> TranscribeFileAsync(
        string fileUri,
        string mimeType,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams transcription of inline audio bytes, yielding each named SSE event as it arrives. For
    /// every clip this library was live-tested against (all a few seconds long), the whole transcript
    /// arrived as a single <c>step.delta</c> event; whether long audio arrives incrementally across
    /// multiple deltas was not confirmed (see <c>PLAN-stt.md</c> §7). The final
    /// <see cref="InteractionStreamEvent.Interaction"/> (on the <c>interaction.completed</c> event)
    /// carries the complete transcript and <see cref="Interaction.Usage"/>.
    /// </summary>
    IAsyncEnumerable<InteractionStreamEvent> StreamTranscribeAsync(
        byte[] audioBytes,
        string mimeType,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls a transcription by ID. Every live call made while building this feature returned
    /// <c>"completed"</c> synchronously from the initial call (see <see cref="TranscribeAsync"/>), but
    /// this exists in case long audio behaves differently; see <c>PLAN-stt.md</c> §7.
    /// </summary>
    Task<Interaction> GetInteractionAsync(string id, CancellationToken cancellationToken = default);
}
