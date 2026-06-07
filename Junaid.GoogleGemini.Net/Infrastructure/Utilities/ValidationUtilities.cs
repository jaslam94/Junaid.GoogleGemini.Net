using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Infrastructure.Utilities
{
    /// <summary>
    /// Utility class for input validation and content verification
    /// </summary>
    public static class ValidationUtilities
    {
        #region Cached Arrays

        /// <summary>
        /// Cached array of all valid models (content generation + embedding models) to avoid repeated allocations
        /// </summary>
        private static readonly string[] _allValidModels = GeminiConstants.Models.ContentGenerationModels
            .Concat(GeminiConstants.Models.EmbeddingModels)
            .ToArray();

        /// <summary>
        /// Cached array of valid message roles to avoid repeated allocations
        /// </summary>
        private static readonly string[] _validRoles = { "user", "model" };

        #endregion

        #region Text Validation

        /// <summary>
        /// Validates text input with comprehensive checks
        /// </summary>
        /// <param name="text">Text to validate</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <param name="maxLength">Maximum allowed length</param>
        /// <param name="allowEmpty">Whether to allow empty strings</param>
        /// <exception cref="ArgumentException">Thrown for invalid input</exception>
        public static void ValidateTextInput(
            string? text, 
            string paramName = "text", 
            int maxLength = GeminiConstants.Limits.MaxTextLength,
            bool allowEmpty = false)
        {
            if (text == null)
            {
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null");
            }

            if (!allowEmpty && string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException($"{paramName} cannot be empty or whitespace", paramName);
            }

            if (text.Length > maxLength)
            {
                throw new ArgumentException(
                    $"{paramName} exceeds maximum length of {maxLength:N0} characters (actual: {text.Length:N0})", 
                    paramName);
            }
        }

        /// <summary>
        /// Validates multiple text inputs
        /// </summary>
        /// <param name="texts">Array of texts to validate</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <param name="maxLength">Maximum allowed length per text</param>
        /// <param name="maxCount">Maximum number of texts allowed</param>
        /// <exception cref="ArgumentException">Thrown for invalid input</exception>
        public static void ValidateTextArray(
            string[]? texts,
            string paramName = "texts",
            int maxLength = GeminiConstants.Limits.MaxEmbeddingTextLength,
            int maxCount = GeminiConstants.Limits.MaxEmbeddingBatchSize)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null");
            }

            if (texts.Length == 0)
            {
                throw new ArgumentException($"{paramName} cannot be empty", paramName);
            }

            if (texts.Length > maxCount)
            {
                throw new ArgumentException(
                    $"{paramName} exceeds maximum count of {maxCount} (actual: {texts.Length})",
                    paramName);
            }

            for (int i = 0; i < texts.Length; i++)
            {
                try
                {
                    ValidateTextInput(texts[i], $"{paramName}[{i}]", maxLength);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Text at index {i}: {ex.Message}", paramName, ex);
                }
            }

            // Check for duplicates
            var duplicates = texts
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .Take(3)
                .ToList();

            if (duplicates.Any())
            {
                throw new ArgumentException(
                    $"Duplicate texts found in {paramName}: {string.Join(", ", duplicates)}...",
                    paramName);
            }
        }

        #endregion

        #region Message Validation

        /// <summary>
        /// Validates chat messages array
        /// </summary>
        /// <param name="messages">Messages to validate</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <exception cref="ArgumentException">Thrown for invalid messages</exception>
        public static void ValidateMessages(MessageObject[]? messages, string paramName = "messages")
        {
            if (messages == null)
            {
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null");
            }

            if (messages.Length == 0)
            {
                throw new ArgumentException($"{paramName} cannot be empty", paramName);
            }

            if (messages.Length > GeminiConstants.Limits.MaxChatMessages)
            {
                throw new ArgumentException(
                    $"Too many messages. Maximum allowed: {GeminiConstants.Limits.MaxChatMessages}, actual: {messages.Length}",
                    paramName);
            }
            
            for (int i = 0; i < messages.Length; i++)
            {
                var message = messages[i];
                if (message == null)
                {
                    throw new ArgumentException($"Message at index {i} cannot be null", paramName);
                }

                if (string.IsNullOrWhiteSpace(message.Role))
                {
                    throw new ArgumentException($"Message role at index {i} cannot be null or empty", paramName);
                }

                if (!_validRoles.Contains(message.Role.ToLowerInvariant()))
                {
                    throw new ArgumentException(
                        $"Invalid message role '{message.Role}' at index {i}. Must be 'user' or 'model'",
                        paramName);
                }

                try
                {
                    ValidateTextInput(message.Text, $"messages[{i}].Text", GeminiConstants.Limits.MaxMessageLength);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Message at index {i}: {ex.Message}", paramName, ex);
                }
            }
        }

        #endregion

        #region File Validation

        /// <summary>
        /// Validates file object for API requirements
        /// </summary>
        /// <param name="fileObject">File object to validate</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <exception cref="ArgumentException">Thrown for invalid files</exception>
        public static void ValidateFileObject(FileObject? fileObject, string paramName = "file")
        {
            if (fileObject == null)
            {
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null");
            }

            if (string.IsNullOrWhiteSpace(fileObject.FileName))
            {
                throw new ArgumentException("File name cannot be null or empty", paramName);
            }

            if (fileObject.FileContent == null || fileObject.FileContent.Length == 0)
            {
                throw new ArgumentException("File content cannot be null or empty", paramName);
            }

            // Use FileUtilities for comprehensive validation
            FileUtilities.ValidateImageFile(fileObject.FileContent, fileObject.FileName);
        }

        #endregion

        #region Model Validation

        /// <summary>
        /// Validates model name
        /// </summary>
        /// <param name="modelName">Model name to validate</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <exception cref="ArgumentException">Thrown for invalid model names</exception>
        public static void ValidateModelName(string? modelName, string paramName = "model")
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", paramName);
            }

            if (!_allValidModels.Contains(modelName))
            {
                throw new ArgumentException(
                    $"Invalid model '{modelName}'. Valid models are: {string.Join(", ", _allValidModels)}",
                    paramName);
            }
        }

        /// <summary>
        /// Validates embedding model name
        /// </summary>
        /// <param name="modelName">Model name to validate</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <exception cref="ArgumentException">Thrown for invalid model names</exception>
        public static void ValidateEmbeddingModel(string? modelName, string paramName = "model")
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", paramName);
            }

            if (!GeminiConstants.Models.EmbeddingModels.Contains(modelName))
            {
                throw new ArgumentException(
                    $"Invalid embedding model '{modelName}'. Valid models are: {string.Join(", ", GeminiConstants.Models.EmbeddingModels)}",
                    paramName);
            }
        }

        #endregion

        #region Response Validation

        /// <summary>
        /// Validates API response for content generation
        /// </summary>
        /// <param name="response">Response to validate</param>
        /// <param name="operation">Operation name for error messages</param>
        /// <exception cref="InvalidOperationException">Thrown for invalid responses</exception>
        public static void ValidateContentResponse(GenerateContentResponse? response, string operation = "operation")
        {
            if (response?.Candidates == null || response.Candidates.Length == 0)
            {
                throw new InvalidOperationException($"No content was generated for {operation}");
            }

            // NOTE: We no longer throw exceptions for blocked content as this is a normal API behavior
            // when safety filters are applied. The response should be processed normally and the 
            // application can check for blocked content using the safety ratings if needed.
        }

        /// <summary>
        /// Validates embedding response
        /// </summary>
        /// <param name="response">Embedding response to validate</param>
        /// <param name="operation">Operation name for error messages</param>
        /// <exception cref="InvalidOperationException">Thrown for invalid responses</exception>
        public static void ValidateEmbeddingResponse(EmbedContentResponse? response, string operation = "embedding")
        {
            if (response?.Embedding?.Values == null || response.Embedding.Values.Length == 0)
            {
                throw new InvalidOperationException($"No embedding was generated for {operation}");
            }

            if (response.Embedding.Values.Length < 50) // Reasonable minimum dimension count
            {
                throw new InvalidOperationException($"Generated embedding has unexpectedly low dimensions ({response.Embedding.Values.Length}) for {operation}");
            }
        }

        /// <summary>
        /// Validates batch embedding response
        /// </summary>
        /// <param name="response">Batch embedding response to validate</param>
        /// <param name="expectedCount">Expected number of embeddings</param>
        /// <param name="operation">Operation name for error messages</param>
        /// <exception cref="InvalidOperationException">Thrown for invalid responses</exception>
        public static void ValidateBatchEmbeddingResponse(
            BatchEmbedContentResponse? response, 
            int expectedCount, 
            string operation = "batch embedding")
        {
            if (response?.Embeddings == null || response.Embeddings.Length == 0)
            {
                throw new InvalidOperationException($"No embeddings were generated for {operation}");
            }

            if (response.Embeddings.Length != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedCount} embeddings but got {response.Embeddings.Length} for {operation}");
            }

            for (int i = 0; i < response.Embeddings.Length; i++)
            {
                try
                {
                    ValidateEmbeddingResponse(
                        new EmbedContentResponse { Embedding = response.Embeddings[i] },
                        $"{operation}[{i}]");
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException($"Embedding at index {i}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Stream Handler Validation

        /// <summary>
        /// Validates stream response handler
        /// </summary>
        /// <param name="handleStreamResponse">Handler to validate</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <exception cref="ArgumentNullException">Thrown for null handlers</exception>
        public static void ValidateStreamHandler(Action<string>? handleStreamResponse, string paramName = "handleStreamResponse")
        {
            if (handleStreamResponse == null)
            {
                throw new ArgumentNullException(paramName, "Stream response handler cannot be null");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Creates a summary of validation results
        /// </summary>
        /// <param name="validationResults">Collection of validation results</param>
        /// <returns>Summary string</returns>
        public static string CreateValidationSummary(IEnumerable<(bool IsValid, string Message)> validationResults)
        {
            var results = validationResults.ToList();
            var validCount = results.Count(r => r.IsValid);
            var invalidCount = results.Count - validCount;

            var summary = $"Validation Summary: {validCount} passed, {invalidCount} failed";
            
            if (invalidCount > 0)
            {
                var errors = results.Where(r => !r.IsValid).Select(r => r.Message);
                summary += "\nErrors:\n" + string.Join("\n", errors.Select(e => $"- {e}"));
            }

            return summary;
        }

        #endregion
    }
}