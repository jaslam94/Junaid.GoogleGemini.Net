using Junaid.GoogleGemini.Net.Infrastructure.Builders;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Service for creative writing tasks using Gemini
    /// </summary>
    public class CreativeWritingService : Service
    {
        private const string MODEL_NAME = "gemini-pro";

        public CreativeWritingService(IGeminiClient geminiClient) : base(geminiClient)
        {
        }

        /// <summary>
        /// Generates a story based on the given prompt
        /// </summary>
        public async Task<GenerateContentResponse> GenerateStoryAsync(
            string prompt,
            string genre = "",
            int? wordCount = null,
            CancellationToken cancellationToken = default)
        {
            var storyPrompt = "Write a creative story";
            if (!string.IsNullOrEmpty(genre))
                storyPrompt += $" in the {genre} genre";
            if (wordCount.HasValue)
                storyPrompt += $" in approximately {wordCount} words";
            storyPrompt += $" based on this prompt: {prompt}";

            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText(storyPrompt)
                .AddMessage()
                .WithTemperature(0.9f)
                .WithTopP(0.8f)
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Generates poetry based on the given theme
        /// </summary>
        public async Task<GenerateContentResponse> GeneratePoetryAsync(
            string theme,
            string style = "",
            CancellationToken cancellationToken = default)
        {
            var poetryPrompt = "Write a poem";
            if (!string.IsNullOrEmpty(style))
                poetryPrompt += $" in the style of {style}";
            poetryPrompt += $" about: {theme}";

            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText(poetryPrompt)
                .AddMessage()
                .WithTemperature(0.9f)
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Generates a descriptive scene based on the given parameters
        /// </summary>
        public async Task<GenerateContentResponse> GenerateSceneDescriptionAsync(
            string setting,
            string mood = "",
            string focusElements = "",
            CancellationToken cancellationToken = default)
        {
            var prompt = $"Write a vivid and detailed description of this scene: {setting}";
            if (!string.IsNullOrEmpty(mood))
                prompt += $"\nConvey a {mood} mood.";
            if (!string.IsNullOrEmpty(focusElements))
                prompt += $"\nFocus on these elements: {focusElements}";

            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText(prompt)
                .AddMessage()
                .WithTemperature(0.8f)
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Generates character dialogue based on the given context
        /// </summary>
        public async Task<GenerateContentResponse> GenerateDialogueAsync(
            string characters,
            string situation,
            string tone = "",
            CancellationToken cancellationToken = default)
        {
            var prompt = $"Write a dialogue between {characters} in this situation: {situation}";
            if (!string.IsNullOrEmpty(tone))
                prompt += $"\nThe tone should be {tone}.";

            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText(prompt)
                .AddMessage()
                .WithTemperature(0.85f)
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Enhances the given text with more creative and engaging language
        /// </summary>
        public async Task<GenerateContentResponse> EnhanceWritingAsync(
            string text,
            string style = "",
            CancellationToken cancellationToken = default)
        {
            var prompt = "Enhance this text with more creative and engaging language while maintaining its core meaning:";
            if (!string.IsNullOrEmpty(style))
                prompt += $"\nUse a {style} writing style.";
            prompt += $"\n\nOriginal text:\n\"{text}\"";

            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText(prompt)
                .AddMessage()
                .WithTemperature(0.7f)
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }
    }
}
