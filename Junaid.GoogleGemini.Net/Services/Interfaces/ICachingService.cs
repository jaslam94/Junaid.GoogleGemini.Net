using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Manages cached content (context caching). Cache a large, reused payload once, then reference it
    /// by name from later requests (via <c>GeminiRequestOptions.CachedContent</c>) to cut tokens/cost.
    /// </summary>
    public interface ICachingService
    {
        /// <summary>Creates a cached-content entry. The request must set Model and Contents (and a TTL or expiry).</summary>
        Task<CachedContent> CreateAsync(CachedContent content, CancellationToken cancellationToken = default);

        /// <summary>Gets a cached-content entry by name (e.g. "cachedContents/abc" or "abc").</summary>
        Task<CachedContent> GetAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>Lists cached-content entries.</summary>
        Task<CachedContentList> ListAsync(int? pageSize = null, string? pageToken = null, CancellationToken cancellationToken = default);

        /// <summary>Updates the expiry of a cached-content entry (e.g. "600s").</summary>
        Task<CachedContent> UpdateTtlAsync(string name, string ttl, CancellationToken cancellationToken = default);

        /// <summary>Deletes a cached-content entry.</summary>
        Task DeleteAsync(string name, CancellationToken cancellationToken = default);
    }
}
