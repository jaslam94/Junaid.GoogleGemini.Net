using System.Collections.Concurrent;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
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

        // Registered AddTransient, so an instance-level field would not actually achieve "warn once
        // per process" (a new EmbeddingService is created per resolution); static gives the real
        // once-ever semantics, matching GeminiCostGovernor's warn-once-per-unpriced-model intent even
        // though that type happens to be a singleton and can use an instance field.
        private static readonly ConcurrentDictionary<string, bool> _warnedTaskTypeModels = new(StringComparer.Ordinal);

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
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateEmbeddingInputs(model, text);
            WarnIfTaskTypeIgnored(model, options);

            try
            {
                var request = RequestFactory.CreateEmbeddingRequest(text, options);
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
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateBatchInputs(model, texts);
            WarnIfTaskTypeIgnored(model, options);

            try
            {
                var requests = texts.Select(text => new EmbedContentRequest
                {
                    Model = $"models/{model}",
                    Content = RequestFactory.CreateEmbeddingContent(text),
                    TaskType = options?.TaskType,
                    Title = options?.Title,
                    OutputDimensionality = options?.OutputDimensionality
                });

                var batchRequest = new BatchEmbedContentRequest
                {
                    Requests = requests.ToArray()
                };

                var endpoint = $"models/{model}:batchEmbedContents";
                var response = await GeminiClient.PostAsync<BatchEmbedContentRequest, BatchEmbedContentResponse>(
                    endpoint,
                    batchRequest,
                    cancellationToken);

                ValidateBatchEmbeddingResponse(response);
                Logger?.LogDebug("Generated {Count} embeddings with model {Model}", response.Embeddings?.Length ?? 0, model);
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                Logger?.LogError(ex, "Failed to generate batch embeddings with model {Model} for {TextCount} texts", model, texts.Length);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<EmbedContentResponse> EmbedContentAsync(
            string model,
            byte[] mediaBytes,
            string mimeType,
            string? text = null,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateModel(model);
            ValidateMedia(mediaBytes, mimeType);
            WarnIfTaskTypeIgnored(model, options);

            try
            {
                var content = RequestFactory.CreateMultimodalEmbeddingContent(text, mediaBytes, mimeType);
                var request = RequestFactory.CreateEmbeddingRequest(content, options);
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
        public async Task<EmbedContentResponse> EmbedFileAsync(
            string model,
            string fileUri,
            string mimeType,
            string? text = null,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateModel(model);
            if (string.IsNullOrWhiteSpace(fileUri))
            {
                throw new ArgumentException("File URI cannot be null or empty", nameof(fileUri));
            }
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                throw new ArgumentException("MIME type cannot be null or empty", nameof(mimeType));
            }
            WarnIfTaskTypeIgnored(model, options);

            try
            {
                var content = RequestFactory.CreateMultimodalEmbeddingContent(text, fileUri: fileUri, mimeType: mimeType);
                var request = RequestFactory.CreateEmbeddingRequest(content, options);
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
            IReadOnlyList<EmbeddingInput> inputs,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateModel(model);
            ValidateEmbeddingInputsList(inputs);
            WarnIfTaskTypeIgnored(model, options);

            try
            {
                var requests = inputs.Select(input => new EmbedContentRequest
                {
                    Model = $"models/{model}",
                    Content = RequestFactory.CreateMultimodalEmbeddingContent(input.Text, input.MediaBytes, input.MimeType, input.FileUri),
                    TaskType = options?.TaskType,
                    Title = options?.Title,
                    OutputDimensionality = options?.OutputDimensionality
                });

                var batchRequest = new BatchEmbedContentRequest
                {
                    Requests = requests.ToArray()
                };

                var endpoint = $"models/{model}:batchEmbedContents";
                var response = await GeminiClient.PostAsync<BatchEmbedContentRequest, BatchEmbedContentResponse>(
                    endpoint,
                    batchRequest,
                    cancellationToken);

                ValidateBatchEmbeddingResponse(response);
                Logger?.LogDebug("Generated {Count} embeddings with model {Model}", response.Embeddings?.Length ?? 0, model);
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                Logger?.LogError(ex, "Failed to generate batch embeddings with model {Model} for {Count} inputs", model, inputs.Count);
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
            // No allow-list: new embedding models (e.g. gemini-embedding-2) must work without a
            // library update. The API rejects genuinely invalid names.
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(model));
            }
        }

        private static void ValidateMedia(byte[] mediaBytes, string mimeType)
        {
            if (mediaBytes is null || mediaBytes.Length == 0)
            {
                throw new ArgumentException("Media bytes cannot be null or empty", nameof(mediaBytes));
            }
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                throw new ArgumentException("MIME type cannot be null or empty", nameof(mimeType));
            }
        }

        private static void ValidateEmbeddingInputsList(IReadOnlyList<EmbeddingInput> inputs)
        {
            if (inputs is null || inputs.Count == 0)
            {
                throw new ArgumentException("Inputs cannot be null or empty", nameof(inputs));
            }
            if (inputs.Count > MAX_BATCH_SIZE)
            {
                throw new ArgumentException($"Batch size exceeds maximum limit of {MAX_BATCH_SIZE}", nameof(inputs));
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                if (input is null)
                {
                    throw new ArgumentException($"Input at index {i} is null", nameof(inputs));
                }
                if (string.IsNullOrEmpty(input.Text) && input.MediaBytes is null && input.FileUri is null)
                {
                    throw new ArgumentException($"Input at index {i} has neither Text, MediaBytes, nor FileUri set", nameof(inputs));
                }
                if (input.MediaBytes is not null && input.FileUri is not null)
                {
                    throw new ArgumentException($"Input at index {i} sets both MediaBytes and FileUri; set at most one", nameof(inputs));
                }
                if ((input.MediaBytes is not null || input.FileUri is not null) && string.IsNullOrWhiteSpace(input.MimeType))
                {
                    throw new ArgumentException($"Input at index {i} has media but no MimeType", nameof(inputs));
                }
            }
        }

        /// <summary>
        /// Logs a one-time warning (see <see cref="EmbeddingOptions.TaskType"/>'s doc comment for the
        /// live-confirmed finding this exists for) when a caller sets <c>TaskType</c> against
        /// <c>gemini-embedding-2</c> specifically, the one model confirmed to silently ignore it.
        /// </summary>
        private void WarnIfTaskTypeIgnored(string model, EmbeddingOptions? options)
        {
            if (options?.TaskType is not null
                && string.Equals(model, GeminiConstants.Models.GeminiEmbedding2, StringComparison.Ordinal)
                && _warnedTaskTypeModels.TryAdd(model, true))
            {
                Logger?.LogWarning(
                    "EmbeddingOptions.TaskType was set for model {Model}, but this field has no effect " +
                    "on gemini-embedding-2: confirmed live, an identical request with and without it " +
                    "set returned byte-for-byte identical embeddings. It is silently accepted (no " +
                    "error), not applied. Include the task instruction as text in the prompt instead.",
                    model);
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
            if (response?.Embedding?.Values == null || response.Embedding.Values.Length == 0)
            {
                throw new InvalidOperationException("No embedding was generated");
            }

            if (response.Embedding.Values.Length < 50) // Reasonable minimum
            {
                throw new InvalidOperationException("Generated embedding has unexpectedly low dimensions");
            }
        }

        private static void ValidateBatchEmbeddingResponse(BatchEmbedContentResponse response)
        {
            if (response?.Embeddings == null || response.Embeddings.Length == 0)
            {
                throw new InvalidOperationException("No embeddings were generated");
            }

            for (int i = 0; i < response.Embeddings.Length; i++)
            {
                var embedding = response.Embeddings[i];
                if (embedding?.Values == null || embedding.Values.Length == 0)
                {
                    throw new InvalidOperationException($"Embedding at index {i} is invalid");
                }
            }
        }
    }
}