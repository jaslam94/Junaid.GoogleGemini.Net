using System.Runtime.CompilerServices;
using System.Text;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Implements <see cref="IVoiceService"/> over the <c>voices</c> endpoints. They live on the normal
    /// versioned base address, so it reuses the shared <see cref="IGeminiClient"/> (auth, retries, rate limiting
    /// and telemetry all apply).
    /// </summary>
    public class VoiceService : IVoiceService
    {
        private readonly IGeminiClient _client;
        private readonly ILogger<VoiceService> _logger;

        /// <summary>Initializes a new instance of the <see cref="VoiceService"/>.</summary>
        public VoiceService(IGeminiClient client, ILogger<VoiceService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<VoiceList> ListAsync(VoiceListOptions? options = null, CancellationToken cancellationToken = default) =>
            _client.GetAsync<VoiceList>("voices" + BuildQuery(options), cancellationToken);

        /// <inheritdoc/>
        public async IAsyncEnumerable<Voice> ListAllAsync(
            VoiceListOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var page = options?.Clone() ?? new VoiceListOptions();
            string? previousToken = null;

            while (true)
            {
                var result = await ListAsync(page, cancellationToken);
                foreach (var voice in result.Voices ?? new List<Voice>())
                {
                    yield return voice;
                }

                var next = result.NextPageToken;
                // Stop on the last page, and never loop if Google ever repeats a token.
                if (string.IsNullOrEmpty(next) || next == previousToken)
                {
                    yield break;
                }

                previousToken = next;
                page.PageToken = next;
            }
        }

        /// <inheritdoc/>
        public Task<Voice> GetAsync(string id, CancellationToken cancellationToken = default) =>
            _client.GetAsync<Voice>(Normalize(id), cancellationToken);

        /// <inheritdoc/>
        public Task<Voice> CreateDesignedAsync(DesignedVoiceRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                throw new ArgumentException(
                    "A description is required, for example \"A calm, friendly narrator with a clear American accent.\"",
                    nameof(request));
            }

            var body = new CreateVoiceRequest
            {
                Store = true,
                Voice = new CreateVoiceBody
                {
                    Model = request.Model,
                    Type = "prompted",
                    DisplayName = request.DisplayName,
                    Gender = request.Gender,
                    LanguageCode = request.LanguageCode,
                    Prompted = new VoicePrompted { Input = request.Description },
                },
            };

            return _client.PostAsync<CreateVoiceRequest, Voice>("voices", body, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<Voice> CreateReplicatedAsync(ReplicatedVoiceRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (request.SourceAudio is not { Length: > 0 })
            {
                throw new ArgumentException("Source audio is required: a 10 to 30 second clip of the speaker.", nameof(request));
            }
            if (request.ConsentAudio is not { Length: > 0 })
            {
                throw new ArgumentException(
                    "Consent audio is required: the same speaker reciting Google's consent statement. " +
                    "This library does not create or fake one.",
                    nameof(request));
            }

            var body = new CreateVoiceRequest
            {
                Store = true,
                Voice = new CreateVoiceBody
                {
                    Model = request.Model,
                    Type = "replicated",
                    DisplayName = request.DisplayName,
                    Replicated = new CreateVoiceReplicated
                    {
                        SourceAudio = new VoiceSampleAudio
                        {
                            MimeType = request.SourceAudioMimeType,
                            Data = Convert.ToBase64String(request.SourceAudio),
                        },
                        ConsentAudio = new VoiceSampleAudio
                        {
                            MimeType = request.ConsentAudioMimeType,
                            Data = Convert.ToBase64String(request.ConsentAudio),
                        },
                    },
                },
            };

            return _client.PostAsync<CreateVoiceRequest, Voice>("voices", body, cancellationToken);
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
            _client.DeleteAsync(Normalize(id), cancellationToken);

        // Accepts "voice_abc", "voices/voice_abc" or a full id, and returns the endpoint path.
        private static string Normalize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A voice id is required, for example \"voice_abc123\".", nameof(id));
            }

            return id.StartsWith("voices/", StringComparison.Ordinal) ? id : $"voices/{Uri.EscapeDataString(id)}";
        }

        // Google's parameter names are snake_case. Each list filter repeats its parameter once per value.
        // Live-verified on 2026-10-10: Google accepts all of these names, and rejects any unknown name with HTTP 400.
        private static string BuildQuery(VoiceListOptions? options)
        {
            if (options is null)
            {
                return string.Empty;
            }

            var query = new StringBuilder();
            void Add(string name, string value)
            {
                query.Append(query.Length == 0 ? '?' : '&').Append(name).Append('=').Append(Uri.EscapeDataString(value));
            }
            void AddAll(string name, IList<string>? values)
            {
                if (values is null) return;
                foreach (var value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value)) Add(name, value);
                }
            }

            AddAll("language_code", options.LanguageCodes);
            AddAll("region_code", options.RegionCodes);
            AddAll("accent", options.Accents);
            AddAll("gender", options.Genders);
            AddAll("pitch", options.Pitches);
            AddAll("persona", options.Personas);
            AddAll("context", options.Contexts);
            AddAll("type", options.Types);
            if (!string.IsNullOrWhiteSpace(options.Search)) Add("search", options.Search!);
            if (options.PageSize is > 0) Add("page_size", options.PageSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(options.PageToken)) Add("page_token", options.PageToken!);

            return query.ToString();
        }
    }
}
