using Junaid.GoogleGemini.Net.Infrastructure.Builders;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Service for advanced content analysis using Gemini
    /// </summary>
    public class AnalysisService : Service
    {
        private const string MODEL_NAME = "gemini-pro";

        public AnalysisService(IGeminiClient geminiClient) : base(geminiClient)
        {
        }

        /// <summary>
        /// Performs sentiment analysis on the given text
        /// </summary>
        public async Task<GenerateContentResponse> AnalyzeSentimentAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText($"Analyze the sentiment of this text and provide a detailed breakdown:\n\"{text}\"\n" +
                        "Include:\n" +
                        "1. Overall sentiment (positive/negative/neutral)\n" +
                        "2. Confidence score\n" +
                        "3. Key emotional indicators\n" +
                        "4. Notable phrases that influence the sentiment")
                .AddMessage()
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Extracts key topics and themes from the text
        /// </summary>
        public async Task<GenerateContentResponse> ExtractTopicsAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText($"Extract and analyze the main topics and themes from this text:\n\"{text}\"\n" +
                        "Provide:\n" +
                        "1. Main topics identified\n" +
                        "2. Key themes and their context\n" +
                        "3. Related subtopics\n" +
                        "4. Topic relationships and hierarchy")
                .AddMessage()
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Analyzes the writing style and provides readability metrics
        /// </summary>
        public async Task<GenerateContentResponse> AnalyzeWritingStyleAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText($"Analyze the writing style and readability of this text:\n\"{text}\"\n" +
                        "Include:\n" +
                        "1. Writing style characteristics\n" +
                        "2. Readability level and scores\n" +
                        "3. Vocabulary complexity\n" +
                        "4. Sentence structure analysis\n" +
                        "5. Suggestions for improvement")
                .AddMessage()
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Extracts structured data from unstructured text
        /// </summary>
        public async Task<GenerateContentResponse> ExtractStructuredDataAsync(
            string text,
            string dataFormat = "JSON",
            CancellationToken cancellationToken = default)
        {
            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText($"Extract structured information from this text and format it as {dataFormat}:\n\"{text}\"\n" +
                        "Extract all relevant:\n" +
                        "1. Named entities (people, organizations, locations)\n" +
                        "2. Dates and times\n" +
                        "3. Key-value pairs\n" +
                        "4. Relationships between entities\n" +
                        $"Provide the result in clean, well-formatted {dataFormat}")
                .AddMessage()
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }

        /// <summary>
        /// Analyzes the semantic similarity between two texts
        /// </summary>
        public async Task<GenerateContentResponse> AnalyzeSimilarityAsync(
            string text1,
            string text2,
            CancellationToken cancellationToken = default)
        {
            var request = new ContentRequestBuilder()
                .WithRole("user")
                .AddText($"Analyze the semantic similarity between these two texts:\n\nText 1:\n\"{text1}\"\n\nText 2:\n\"{text2}\"\n\n" +
                        "Provide:\n" +
                        "1. Similarity score (0-100%)\n" +
                        "2. Key similarities identified\n" +
                        "3. Notable differences\n" +
                        "4. Shared themes or concepts")
                .AddMessage()
                .Build();

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                $"/models/{MODEL_NAME}/generateContent",
                request,
                cancellationToken);
        }
    }
}
