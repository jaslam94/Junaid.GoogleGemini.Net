using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Service for generating embeddings using Gemini API
    /// </summary>
    public class EmbeddingService : Service, IEmbeddingService
    {
        private const int MAX_TEXT_LENGTH = 20000;
        private const int MAX_BATCH_SIZE = 100;
        private static readonly string[] ValidModels = { "embedding-001", "text-embedding-004" };

        /// <summary>
        /// Initializes a new instance of the EmbeddingService
        /// </summary>
        public EmbeddingService(
            IGeminiClient geminiClient,
            ILogger<EmbeddingService> logger,
            IOptions<GeminiOptions> options) : base(geminiClient, logger, options, null)
        {
        }

        /// <inheritdoc/>
        public async Task<EmbedContentResponse> EmbedContentAsync(
            string model,
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidateEmbeddingInputs(model, text);

            try
            {
                var request = RequestFactory.CreateEmbeddingRequest(text);
                var endpoint = $"models/{model}:embedContent";
                
                var response = await GeminiClient.PostAsync<SingleEmbedContentRequest, EmbedContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateEmbeddingResponse(response);
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                Logger?.LogError(ex, "Failed to generate embedding with model {Model}", model);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<BatchEmbedContentResponse> BatchEmbedContentAsync(
            string model,
            string[] texts,
            CancellationToken cancellationToken = default)
        {
            ValidateBatchInputs(model, texts);

            try
            {
                var requests = texts.Select(text => new EmbedContentRequest
                {
                    model = $"models/{model}",
                    content = RequestFactory.CreateEmbeddingContent(text)
                });

                var batchRequest = new BatchEmbedContentRequest
                {
                    requests = requests.ToArray()
                };

                var endpoint = $"models/{model}:batchEmbedContents";
                var response = await GeminiClient.PostAsync<BatchEmbedContentRequest, BatchEmbedContentResponse>(
                    endpoint,
                    batchRequest,
                    cancellationToken);

                ValidateBatchEmbeddingResponse(response);
                Logger?.LogDebug("Generated {Count} embeddings with model {Model}", response.embeddings?.Length ?? 0, model);
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                Logger?.LogError(ex, "Failed to generate batch embeddings with model {Model} for {TextCount} texts", model, texts.Length);
                throw;
            }
        }

        private void ValidateEmbeddingInputs(string model, string text)
        {
            ValidateModel(model);
            ValidateTextInput(text, nameof(text), MAX_TEXT_LENGTH);
        }

        private void ValidateBatchInputs(string model, string[] texts)
        {
            ValidateModel(model);

            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts), "Texts array cannot be null");
            }

            if (texts.Length == 0)
            {
                throw new ArgumentException("Texts array cannot be empty", nameof(texts));
            }

            if (texts.Length > MAX_BATCH_SIZE)
            {
                throw new ArgumentException(
                    $"Batch size exceeds maximum limit of {MAX_BATCH_SIZE}",
                    nameof(texts));
            }

            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ArgumentException($"Text at index {i} cannot be null or empty", nameof(texts));
                }

                if (text.Length > MAX_TEXT_LENGTH)
                {
                    throw new ArgumentException(
                        $"Text at index {i} exceeds maximum length of {MAX_TEXT_LENGTH:N0} characters",
                        nameof(texts));
                }
            }

            ValidateNoDuplicates(texts);
        }

        private static void ValidateModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(model));
            }

            if (!ValidModels.Contains(model))
            {
                throw new ArgumentException(
                    $"Invalid model '{model}'. Supported models: {string.Join(", ", ValidModels)}",
                    nameof(model));
            }
        }

        private static void ValidateNoDuplicates(string[] texts)
        {
            var duplicates = texts
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any())
            {
                throw new ArgumentException(
                    $"Duplicate texts found in batch: {string.Join(", ", duplicates.Take(3))}...",
                    nameof(texts));
            }
        }

        private static void ValidateEmbeddingResponse(EmbedContentResponse response)
        {
            if (response?.embedding?.values == null || response.embedding.values.Length == 0)
            {
                throw new InvalidOperationException("No embedding was generated");
            }

            if (response.embedding.values.Length < 50) // Reasonable minimum
            {
                throw new InvalidOperationException("Generated embedding has unexpectedly low dimensions");
            }
        }

        private static void ValidateBatchEmbeddingResponse(BatchEmbedContentResponse response)
        {
            if (response?.embeddings == null || response.embeddings.Length == 0)
            {
                throw new InvalidOperationException("No embeddings were generated");
            }

            for (int i = 0; i < response.embeddings.Length; i++)
            {
                var embedding = response.embeddings[i];
                if (embedding?.values == null || embedding.values.Length == 0)
                {
                    throw new InvalidOperationException($"Embedding at index {i} is invalid");
                }
            }
        }
    }
}