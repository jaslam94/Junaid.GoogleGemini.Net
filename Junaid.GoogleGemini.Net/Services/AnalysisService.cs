using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// DEPRECATED: Use IGeminiService for unified content analysis. Will be removed in v7.0.0
    /// Service for advanced content analysis using Gemini
    /// </summary>
    [Obsolete("Use IGeminiService for content analysis operations. This service will be removed in v7.0.0")]
    public class AnalysisService : Service
    {
        /// <summary>
        /// Initializes a new instance of the AnalysisService
        /// </summary>
        public AnalysisService(
            IGeminiClient geminiClient,
            ILogger<AnalysisService> logger,
            IOptions<GeminiOptions> options,
            ISafetyService safetyService) : base(geminiClient, logger, options, safetyService)
        {
        }

        /// <summary>
        /// DEPRECATED: Use IGeminiService.GenerateAsync() with analysis prompts instead
        /// </summary>
        [Obsolete("Use IGeminiService.GenerateAsync() with analysis prompts instead")]
        public async Task<GenerateContentResponse> AnalyzeSentimentAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(text, nameof(text), GeminiConstants.Limits.MaxTextLength);

            var prompt = $"Analyze the sentiment of this text and provide a detailed breakdown:\n\"{text}\"\n" +
                        "Include:\n" +
                        "1. Overall sentiment (positive/negative/neutral)\n" +
                        "2. Confidence score\n" +
                        "3. Key emotional indicators\n" +
                        "4. Notable phrases that influence the sentiment";

            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "sentiment analysis",
                endpoint,
                request,
                new { TextLength = text.Length },
                cancellationToken);
        }

        /// <summary>
        /// DEPRECATED: Use IGeminiService.GenerateAsync() with topic extraction prompts instead
        /// </summary>
        [Obsolete("Use IGeminiService.GenerateAsync() with topic extraction prompts instead")]
        public async Task<GenerateContentResponse> ExtractTopicsAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(text, nameof(text), GeminiConstants.Limits.MaxTextLength);

            var prompt = $"Extract and analyze the main topics and themes from this text:\n\"{text}\"\n" +
                        "Provide:\n" +
                        "1. Main topics identified\n" +
                        "2. Key themes and their context\n" +
                        "3. Related subtopics\n" +
                        "4. Topic relationships and hierarchy";

            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "topic extraction",
                endpoint,
                request,
                new { TextLength = text.Length },
                cancellationToken);
        }

        /// <summary>
        /// DEPRECATED: Use IGeminiService.GenerateAsync() with writing analysis prompts instead
        /// </summary>
        [Obsolete("Use IGeminiService.GenerateAsync() with writing analysis prompts instead")]
        public async Task<GenerateContentResponse> AnalyzeWritingStyleAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(text, nameof(text), GeminiConstants.Limits.MaxTextLength);

            var prompt = $"Analyze the writing style and readability of this text:\n\"{text}\"\n" +
                        "Include:\n" +
                        "1. Writing style characteristics\n" +
                        "2. Readability level and scores\n" +
                        "3. Vocabulary complexity\n" +
                        "4. Sentence structure analysis\n" +
                        "5. Suggestions for improvement";

            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "writing style analysis",
                endpoint,
                request,
                new { TextLength = text.Length },
                cancellationToken);
        }

        /// <summary>
        /// DEPRECATED: Use IGeminiService.GenerateAsync() with data extraction prompts instead
        /// </summary>
        [Obsolete("Use IGeminiService.GenerateAsync() with data extraction prompts instead")]
        public async Task<GenerateContentResponse> ExtractStructuredDataAsync(
            string text,
            string dataFormat = "JSON",
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(text, nameof(text), GeminiConstants.Limits.MaxTextLength);

            var prompt = $"Extract structured information from this text and format it as {dataFormat}:\n\"{text}\"\n" +
                        "Extract all relevant:\n" +
                        "1. Named entities (people, organizations, locations)\n" +
                        "2. Dates and times\n" +
                        "3. Key-value pairs\n" +
                        "4. Relationships between entities\n" +
                        $"Provide the result in clean, well-formatted {dataFormat}";

            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "data extraction",
                endpoint,
                request,
                new { TextLength = text.Length, Format = dataFormat },
                cancellationToken);
        }

        /// <summary>
        /// DEPRECATED: Use IGeminiService.GenerateAsync() with similarity analysis prompts instead
        /// </summary>
        [Obsolete("Use IGeminiService.GenerateAsync() with similarity analysis prompts instead")]
        public async Task<GenerateContentResponse> AnalyzeSimilarityAsync(
            string text1,
            string text2,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(text1, nameof(text1), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateTextInput(text2, nameof(text2), GeminiConstants.Limits.MaxTextLength);

            var prompt = $"Analyze the semantic similarity between these two texts:\n\nText 1:\n\"{text1}\"\n\nText 2:\n\"{text2}\"\n\n" +
                        "Provide:\n" +
                        "1. Similarity score (0-100%)\n" +
                        "2. Key similarities identified\n" +
                        "3. Notable differences\n" +
                        "4. Shared themes or concepts";

            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "similarity analysis",
                endpoint,
                request,
                new { Text1Length = text1.Length, Text2Length = text2.Length },
                cancellationToken);
        }
    }
}
