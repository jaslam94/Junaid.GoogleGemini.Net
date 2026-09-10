using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;

namespace Junaid.GoogleGemini.Net.Services;

/// <summary>
/// Implements <see cref="ITranscriptionService"/> via the Interactions API
/// (<see cref="IGeminiClient.PostInteractionAsync"/>/<see cref="IGeminiClient.StreamInteractionAsync"/>).
/// Uses the shared <see cref="IGeminiClient"/> (unlike <c>BatchService</c>/<c>FileService</c>, which
/// use their own dedicated <see cref="HttpClient"/>): transcription is a normal interactive call that
/// should go through the same rate limiter and cost governor as every other generation call, not opt
/// out of them the way batch jobs deliberately do.
/// </summary>
public class TranscriptionService : ITranscriptionService
{
    private readonly IGeminiClient _client;

    /// <summary>Initializes a new instance of the <see cref="TranscriptionService"/>.</summary>
    public TranscriptionService(IGeminiClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc/>
    public Task<Interaction> TranscribeAsync(
        byte[] audioBytes,
        string mimeType,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (audioBytes is null || audioBytes.Length == 0)
        {
            throw new ArgumentException("Audio bytes are required.", nameof(audioBytes));
        }
        ValidateMimeType(mimeType);

        var input = new InteractionInput
        {
            Type = "audio",
            Data = Convert.ToBase64String(audioBytes),
            MimeType = mimeType,
        };

        var request = BuildRequest(input, options);
        return _client.PostInteractionAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Interaction> TranscribeFileAsync(
        string fileUri,
        string mimeType,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUri))
        {
            throw new ArgumentException("File URI is required.", nameof(fileUri));
        }
        ValidateMimeType(mimeType);

        var input = new InteractionInput
        {
            Type = "audio",
            Uri = fileUri,
            MimeType = mimeType,
        };

        var request = BuildRequest(input, options);
        return _client.PostInteractionAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<InteractionStreamEvent> StreamTranscribeAsync(
        byte[] audioBytes,
        string mimeType,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (audioBytes is null || audioBytes.Length == 0)
        {
            throw new ArgumentException("Audio bytes are required.", nameof(audioBytes));
        }
        ValidateMimeType(mimeType);

        var input = new InteractionInput
        {
            Type = "audio",
            Data = Convert.ToBase64String(audioBytes),
            MimeType = mimeType,
        };

        var request = BuildRequest(input, options);
        return _client.StreamInteractionAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Interaction> GetInteractionAsync(string id, CancellationToken cancellationToken = default) =>
        _client.GetInteractionAsync(id, cancellationToken);

    private static InteractionRequest BuildRequest(InteractionInput input, TranscriptionOptions? options)
    {
        options ??= new TranscriptionOptions();
        options.Validate();
        ValidationUtilities.ValidateModelName(options.Model);

        TranscriptionConfig? transcriptionConfig = null;
        if (options.LanguageCodes is { Count: > 0 } || options.CustomVocabulary is { Count: > 0 } ||
            options.DiarizationMode is not null || options.TimestampGranularities is { Count: > 0 })
        {
            transcriptionConfig = new TranscriptionConfig
            {
                LanguageCodes = options.LanguageCodes?.ToList(),
                CustomVocabulary = options.CustomVocabulary?.ToList(),
                DiarizationMode = options.DiarizationMode,
                TimestampGranularities = options.TimestampGranularities?.ToList(),
            };
        }

        return new InteractionRequest
        {
            Model = options.Model,
            Input = [input],
            GenerationConfig = transcriptionConfig is null
                ? null
                : new InteractionGenerationConfig { TranscriptionConfig = transcriptionConfig },
        };
    }

    private static void ValidateMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException("MIME type is required.", nameof(mimeType));
        }
    }
}
