using Junaid.GoogleGemini.Net.Infrastructure.Builders;
using Junaid.GoogleGemini.Net.Infrastructure.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Service for text-only operations using Gemini API
    /// </summary>
    public class TextService : Service, ITextService
    {
        /// <summary>
        /// Initializes a new instance of the TextService
        /// </summary>
        public TextService(
            IGeminiClient geminiClient,
            ILogger<TextService> logger,
            IOptions<GeminiOptions> options,
            ISafetyService safetyService) : base(geminiClient, logger, options, safetyService)
        {
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> GenereateContentAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidateTextInput(text, nameof(text));

            try
            {
                LogOperationStart("text content generation", new { TextLength = text.Length });

                var request = GeminiClient.CreateTextRequest(text).Build();
                var endpoint = $"/models/{Options.DefaultModel}:generateContent";
                
                var response = await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateResponse(response, "text content generation");
                LogOperationSuccess("text content generation");
                
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, "text content generation");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task StreamGenereateContentAsync(
            string text,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default)
        {
            ValidateTextInput(text, nameof(text));
            ValidateStreamHandler(handleStreamResponse);

            try
            {
                LogOperationStart("text content streaming", new { TextLength = text.Length });

                var request = GeminiClient.CreateStreamingRequest(
                    GeminiClient.CreateTextRequest(text)
                ).Build();

                var endpoint = $"/models/{Options.DefaultModel}:streamGenerateContent";
                await foreach (var data in GeminiClient.SendAsync(endpoint, request).WithCancellation(cancellationToken))
                {
                    handleStreamResponse(data);
                }

                LogOperationSuccess("text content streaming");
            }
            catch (Exception ex)
            {
                LogOperationError(ex, "text content streaming");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidateTextInput(text, nameof(text));

            try
            {
                LogOperationStart("token counting", new { TextLength = text.Length });

                var request = new ContentRequestBuilder()
                    .WithRole("user")
                    .AddText(text)
                    .AddMessage()
                    .Build();

                var endpoint = $"/models/{Options.DefaultModel}:countTokens";
                var response = await GeminiClient.PostAsync<GenerateContentRequest, CountTokensResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                LogOperationSuccess("token counting", new { TokenCount = response.totalTokens });
                return response;
            }
            catch (Exception ex)
            {
                LogOperationError(ex, "token counting");
                throw;
            }
        }
    }
}