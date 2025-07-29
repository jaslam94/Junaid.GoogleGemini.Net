using Junaid.GoogleGemini.Net.Infrastructure.Builders;
using Junaid.GoogleGemini.Net.Infrastructure.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure.Helpers;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Service for vision-related operations using Gemini API
    /// </summary>
    public class VisionService : Service, IVisionService
    {
        private const string MODEL_NAME = "gemini-pro-vision";
        private const int MAX_IMAGE_SIZE = 20 * 1024 * 1024; // 20MB
        private const int MAX_TEXT_LENGTH = 50000; // Vision requests typically have shorter text
        private static readonly string[] SupportedImageTypes = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        /// <summary>
        /// Initializes a new instance of the VisionService
        /// </summary>
        public VisionService(
            IGeminiClient geminiClient,
            ILogger<VisionService> logger,
            IOptions<GeminiOptions> options,
            ISafetyService safetyService) : base(geminiClient, logger, options, safetyService)
        {
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> GenereateContentAsync(
            string text,
            FileObject fileObject,
            CancellationToken cancellationToken = default)
        {
            ValidateVisionInputs(text, fileObject);

            try
            {
                LogOperationStart("vision content generation", new
                {
                    TextLength = text.Length,
                    ImageSize = fileObject.FileContent.Length,
                    FileName = fileObject.FileName
                });

                var request = GeminiClient.CreateVisionRequest(text, fileObject).Build();
                var endpoint = $"/models/{MODEL_NAME}:generateContent";

                var response = await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateResponse(response, "vision content generation");
                LogOperationSuccess("vision content generation");

                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, "vision content generation");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task StreamGenereateContentAsync(
            string text,
            FileObject fileObject,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default)
        {
            ValidateVisionInputs(text, fileObject);
            ValidateStreamHandler(handleStreamResponse);

            try
            {
                LogOperationStart("vision content streaming", new
                {
                    TextLength = text.Length,
                    ImageSize = fileObject.FileContent.Length
                });

                var request = GeminiClient.CreateStreamingRequest(
                    GeminiClient.CreateVisionRequest(text, fileObject)
                ).Build();

                var endpoint = $"/models/{MODEL_NAME}:streamGenerateContent";
                await foreach (var data in GeminiClient.SendAsync(endpoint, request).WithCancellation(cancellationToken))
                {
                    handleStreamResponse(data);
                }

                LogOperationSuccess("vision content streaming");
            }
            catch (Exception ex)
            {
                LogOperationError(ex, "vision content streaming");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensAsync(
            string text,
            FileObject fileObject,
            CancellationToken cancellationToken = default)
        {
            ValidateVisionInputs(text, fileObject);

            try
            {
                LogOperationStart("vision token counting", new
                {
                    TextLength = text.Length,
                    ImageSize = fileObject.FileContent.Length
                });

                var request = new ContentRequestBuilder()
                    .WithRole("user")
                    .AddText(text)
                    .AddImage(
                        Convert.ToBase64String(fileObject.FileContent),
                        MimeTypeHelper.GetMimeType(fileObject.FileName))
                    .AddMessage()
                    .Build();

                var endpoint = $"/models/{MODEL_NAME}:countTokens";
                var response = await GeminiClient.PostAsync<GenerateContentRequest, CountTokensResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                LogOperationSuccess("vision token counting", new { TokenCount = response.totalTokens });
                return response;
            }
            catch (Exception ex)
            {
                LogOperationError(ex, "vision token counting");
                throw;
            }
        }

        private void ValidateVisionInputs(string text, FileObject fileObject)
        {
            ValidateTextInput(text, nameof(text), MAX_TEXT_LENGTH);
            ValidateFileObject(fileObject);
        }

        private static void ValidateFileObject(FileObject fileObject)
        {
            if (fileObject == null)
            {
                throw new ArgumentNullException(nameof(fileObject), "File object cannot be null");
            }

            if (string.IsNullOrWhiteSpace(fileObject.FileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileObject));
            }

            if (fileObject.FileContent == null || fileObject.FileContent.Length == 0)
            {
                throw new ArgumentException("File content cannot be null or empty", nameof(fileObject));
            }

            if (fileObject.FileContent.Length > MAX_IMAGE_SIZE)
            {
                throw new ArgumentException($"Image size exceeds maximum limit of {MAX_IMAGE_SIZE / (1024 * 1024)}MB", nameof(fileObject));
            }

            var extension = Path.GetExtension(fileObject.FileName).ToLowerInvariant();
            if (!SupportedImageTypes.Contains(extension))
            {
                throw new ArgumentException(
                    $"Unsupported image type: {extension}. Supported types: {string.Join(", ", SupportedImageTypes)}",
                    nameof(fileObject));
            }

            var mimeType = MimeTypeHelper.GetMimeType(fileObject.FileName);
            if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid MIME type for image: {mimeType}", nameof(fileObject));
            }
        }
    }
}