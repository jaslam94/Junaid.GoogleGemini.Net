using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Implements context caching over the <c>cachedContents</c> endpoints. These live on the normal
    /// versioned base address, so it reuses the shared <see cref="IGeminiClient"/>.
    /// </summary>
    public class CachingService : ICachingService
    {
        private readonly IGeminiClient _client;
        private readonly ILogger<CachingService> _logger;

        /// <summary>Initializes a new instance of the <see cref="CachingService"/>.</summary>
        public CachingService(IGeminiClient client, ILogger<CachingService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<CachedContent> CreateAsync(CachedContent content, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            if (string.IsNullOrWhiteSpace(content.Model))
            {
                throw new ArgumentException("CachedContent.Model is required (e.g. 'models/gemini-2.5-flash').", nameof(content));
            }

            return _client.PostAsync<CachedContent, CachedContent>("cachedContents", content, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<CachedContent> GetAsync(string name, CancellationToken cancellationToken = default) =>
            _client.GetAsync<CachedContent>(Normalize(name), cancellationToken);

        /// <inheritdoc/>
        public Task<CachedContentList> ListAsync(int? pageSize = null, string? pageToken = null, CancellationToken cancellationToken = default)
        {
            var query = new List<string>();
            if (pageSize is > 0) query.Add($"pageSize={pageSize}");
            if (!string.IsNullOrEmpty(pageToken)) query.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
            var endpoint = "cachedContents" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

            return _client.GetAsync<CachedContentList>(endpoint, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<CachedContent> UpdateTtlAsync(string name, string ttl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ttl))
            {
                throw new ArgumentException("A TTL is required (e.g. '600s').", nameof(ttl));
            }

            // updateMask restricts the patch to the ttl field.
            var endpoint = $"{Normalize(name)}?updateMask=ttl";
            var body = new CachedContent { Ttl = ttl };
            return _client.PatchAsync<CachedContent, CachedContent>(endpoint, body, cancellationToken);
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
            _client.DeleteAsync(Normalize(name), cancellationToken);

        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Cached-content name is required.", nameof(name));
            }
            return name.StartsWith("cachedContents/", StringComparison.Ordinal) ? name : $"cachedContents/{name}";
        }
    }
}
