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
    /// DEPRECATED: Use IGeminiService.GenerateWithImageAsync() for vision operations. Will be removed in v7.0.0
    /// Service for vision-related operations using Gemini API
    /// </summary>
    [Obsolete("Use IGeminiService.GenerateWithImageAsync() for vision operations. This service will be removed in v7.0.0")]
    public class VisionService : Service, IVisionService
    {
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
            ValidationUtilities.ValidateTextInput(text, nameof(text), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(fileObject, nameof(fileObject));

            var endpoint = $"/models/{GeminiConstants.Models.Gemini15Pro}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateVisionRequest(text, fileObject);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "vision content generation",
                endpoint,
                request,
                new { TextLength = text.Length, ImageSize = fileObject.FileContent.Length, FileName = fileObject.FileName },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StreamGenereateContentAsync(
            string text,
            FileObject fileObject,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(text, nameof(text), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(fileObject, nameof(fileObject));
            ValidationUtilities.ValidateStreamHandler(handleStreamResponse);

            var request = Infrastructure.Factories.RequestFactory.CreateVisionRequest(text, fileObject, streaming: true);
            var endpoint = $"/models/{GeminiConstants.Models.Gemini15Pro}:streamGenerateContent";

            await ExecuteStreamRequestAsync(
                "vision content streaming",
                endpoint,
                request,
                handleStreamResponse,
                new { TextLength = text.Length, ImageSize = fileObject.FileContent.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensAsync(
            string text,
            FileObject fileObject,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(text, nameof(text), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(fileObject, nameof(fileObject));

            var request = Infrastructure.Factories.RequestFactory.CreateVisionRequest(text, fileObject);
            var endpoint = $"/models/{GeminiConstants.Models.Gemini15Pro}:countTokens";

            return await ExecuteRequestAsync<GenerateContentRequest, CountTokensResponse>(
                "vision token counting",
                endpoint,
                request,
                new { TextLength = text.Length, ImageSize = fileObject.FileContent.Length },
                cancellationToken);
        }
    }
}