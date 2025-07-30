using Junaid.GoogleGemini.Net.Infrastructure.Builders;
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

            var endpoint = $"/models/{Options.DefaultModel}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(text);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "text content generation",
                endpoint,
                request,
                new { TextLength = text.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StreamGenereateContentAsync(
            string text,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default)
        {
            ValidateTextInput(text, nameof(text));
            ValidateStreamHandler(handleStreamResponse);

            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText(text)
                .AddMessage()
                .EnableStreaming(true)
                .Build();

            var endpoint = $"/models/{Options.DefaultModel}:streamGenerateContent";
            await ExecuteStreamRequestAsync(
                "text content streaming",
                endpoint,
                request,
                handleStreamResponse,
                new { TextLength = text.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidateTextInput(text, nameof(text));

            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(text);
            var endpoint = $"/models/{Options.DefaultModel}:countTokens";

            return await ExecuteRequestAsync<GenerateContentRequest, CountTokensResponse>(
                "token counting",
                endpoint,
                request,
                new { TextLength = text.Length },
                cancellationToken);
        }
    }
}