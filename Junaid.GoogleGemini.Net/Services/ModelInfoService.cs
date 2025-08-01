using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Service for retrieving model information from Gemini API
    /// </summary>
    public class ModelInfoService : Service, IModelInfoService
    {
        private readonly MemoryCache<ModelInfo> _modelCache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Initializes a new instance of the ModelInfoService
        /// </summary>
        public ModelInfoService(
            IGeminiClient geminiClient,
            ILogger<ModelInfoService> logger,
            IOptions<GeminiOptions> options) : base(geminiClient, logger, options, null) // No safety service needed
        {
            _modelCache = new MemoryCache<ModelInfo>();
        }

        /// <inheritdoc/>
        public async Task<ListModelsResponse> ListModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = "models";
                var response = await GeminiClient.GetAsync<ListModelsResponse>(
                    endpoint,
                    cancellationToken);

                var modelCount = response?.models?.Length ?? 0;
                if (modelCount > 0)
                {
                    // Cache individual models for future GetModelAsync calls
                    foreach (var model in response.models)
                    {
                        if (!string.IsNullOrEmpty(model.name))
                        {
                            _modelCache.Set(model.name, model, _cacheDuration);
                        }
                    }
                }

                Logger?.LogDebug("Retrieved {ModelCount} models from API", modelCount);
                return response ?? new ListModelsResponse { models = Array.Empty<ModelInfo>() };
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to retrieve model list");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelInfo> GetModelAsync(string modelName, CancellationToken cancellationToken = default)
        {
            ValidateTextInput(modelName, nameof(modelName), 100);

            try
            {
                // Try to get from cache first
                if (_modelCache.TryGetValue(modelName, out var cachedModel))
                {
                    return cachedModel;
                }

                var endpoint = $"models/{modelName}";
                var model = await GeminiClient.GetAsync<ModelInfo>(
                    endpoint,
                    cancellationToken);

                if (model == null)
                {
                    throw new InvalidOperationException($"No information found for model: {modelName}");
                }

                // Cache the model info
                _modelCache.Set(modelName, model, _cacheDuration);

                return model;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to retrieve model info for {ModelName}", modelName);
                throw;
            }
        }

        /// <summary>
        /// Simple thread-safe memory cache implementation
        /// </summary>
        private class MemoryCache<T>
        {
            private readonly ConcurrentDictionary<string, (T Value, DateTime Expiry)> _cache = new();

            public void Set(string key, T value, TimeSpan duration)
            {
                _cache[key] = (value, DateTime.UtcNow.Add(duration));
            }

            public bool TryGetValue(string key, out T value)
            {
                if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
                {
                    value = entry.Value;
                    return true;
                }

                // Clean up expired entry
                _cache.TryRemove(key, out _);
                value = default!;
                return false;
            }
        }
    }
}