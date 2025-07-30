using Junaid.GoogleGemini.Net.Infrastructure.Builders;
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
    /// Service for chat-based operations using Gemini API
    /// </summary>
    public class ChatService : Service, IChatService
    {
        private const string MODEL_NAME = "gemini-pro";
        private const int MAX_MESSAGES = 50;
        private const int MAX_MESSAGE_LENGTH = 10000;

        /// <summary>
        /// Initializes a new instance of the ChatService
        /// </summary>
        public ChatService(
            IGeminiClient geminiClient,
            ILogger<ChatService> logger,
            IOptions<GeminiOptions> options,
            ISafetyService safetyService) : base(geminiClient, logger, options, safetyService)
        {
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> GenereateContentAsync(
            MessageObject[] chat,
            CancellationToken cancellationToken = default)
        {
            ValidateChat(chat);

            var endpoint = $"/models/{MODEL_NAME}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateChatRequest(chat);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "chat response generation",
                endpoint,
                request,
                new { MessageCount = chat.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StreamGenereateContentAsync(
            MessageObject[] chat,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default)
        {
            ValidateChat(chat);
            ValidateStreamHandler(handleStreamResponse);

            var request = new ContentRequestBuilder();
            foreach (var message in chat)
            {
                request.WithRole(message.Role)
                       .AddText(message.Text)
                       .AddMessage();
            }
            request.EnableStreaming(true);

            var endpoint = $"/models/{MODEL_NAME}:streamGenerateContent";
            await ExecuteStreamRequestAsync(
                "chat response streaming",
                endpoint,
                request.Build(),
                handleStreamResponse,
                new { MessageCount = chat.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensAsync(
            MessageObject[] chat,
            CancellationToken cancellationToken = default)
        {
            ValidateChat(chat);

            var request = Infrastructure.Factories.RequestFactory.CreateChatRequest(chat);
            var endpoint = $"/models/{MODEL_NAME}:countTokens";

            return await ExecuteRequestAsync<GenerateContentRequest, CountTokensResponse>(
                "chat token counting",
                endpoint,
                request,
                new { MessageCount = chat.Length },
                cancellationToken);
        }

        private static void ValidateChat(MessageObject[] chat)
        {
            if (chat == null)
            {
                throw new ArgumentNullException(nameof(chat), "Chat messages cannot be null");
            }

            if (chat.Length == 0)
            {
                throw new ArgumentException("Chat must contain at least one message", nameof(chat));
            }

            if (chat.Length > MAX_MESSAGES)
            {
                throw new ArgumentException($"Chat exceeds maximum message limit of {MAX_MESSAGES}", nameof(chat));
            }

            var validRoles = new[] { "user", "model" };
            for (int i = 0; i < chat.Length; i++)
            {
                var message = chat[i];
                ValidateChatMessage(message, i, validRoles);
            }

            ValidateConversationFlow(chat);
        }

        private static void ValidateChatMessage(MessageObject message, int index, string[] validRoles)
        {
            if (message == null)
            {
                throw new ArgumentException($"Message at index {index} cannot be null", nameof(message));
            }

            if (string.IsNullOrWhiteSpace(message.Role))
            {
                throw new ArgumentException($"Message role at index {index} cannot be null or empty", nameof(message));
            }

            if (!validRoles.Contains(message.Role.ToLowerInvariant()))
            {
                throw new ArgumentException(
                    $"Invalid message role '{message.Role}' at index {index}. Must be 'user' or 'model'",
                    nameof(message));
            }

            if (string.IsNullOrWhiteSpace(message.Text))
            {
                throw new ArgumentException($"Message text at index {index} cannot be null or empty", nameof(message));
            }

            if (message.Text.Length > MAX_MESSAGE_LENGTH)
            {
                throw new ArgumentException(
                    $"Message at index {index} exceeds maximum length of {MAX_MESSAGE_LENGTH:N0} characters",
                    nameof(message));
            }
        }

        private static void ValidateConversationFlow(MessageObject[] chat)
        {
            // First message should typically be from user
            if (chat.Length > 0 && chat[0].Role.ToLowerInvariant() != "user")
            {
                throw new ArgumentException("First message should typically be from 'user'", nameof(chat));
            }
        }
    }
}