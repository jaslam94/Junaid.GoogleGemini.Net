using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Models.Requests
{
    /// <summary>
    /// Simple options class for configuring Gemini requests
    /// </summary>
    public class GeminiRequestOptions
    {
        /// <summary>
        /// Controls randomness in generation (0.0 to 1.0)
        /// </summary>
        public float? Temperature { get; set; }

        /// <summary>
        /// Maximum number of tokens to generate
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Top-p sampling parameter
        /// </summary>
        public float? TopP { get; set; }

        /// <summary>
        /// Top-k sampling parameter
        /// </summary>
        public int? TopK { get; set; }

        /// <summary>
        /// Model to use for generation (overrides default)
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Custom safety settings for this request
        /// </summary>
        public List<SafetySetting>? SafetySettings { get; set; }

        /// <summary>
        /// Stop sequences for generation
        /// </summary>
        public List<string>? StopSequences { get; set; }

        /// <summary>
        /// Creates options optimized for creative tasks using the recommended model
        /// </summary>
        public static GeminiRequestOptions Creative(string? model = null) => new()
        {
            Temperature = 0.9f,
            TopP = 0.8f,
            TopK = 40,
            Model = model ?? GeminiConstants.Models.Recommended
        };

        /// <summary>
        /// Creates options optimized for factual/precise tasks using the recommended model
        /// </summary>
        public static GeminiRequestOptions Factual(string? model = null) => new()
        {
            Temperature = 0.1f,
            TopP = 0.1f,
            TopK = 1,
            Model = model ?? GeminiConstants.Models.Recommended
        };

        /// <summary>
        /// Creates options optimized for code generation using the recommended model
        /// </summary>
        public static GeminiRequestOptions Code(string? model = null) => new()
        {
            Temperature = 0.1f,
            TopP = 0.1f,
            TopK = 1,
            Model = model ?? GeminiConstants.Models.Recommended
        };

        /// <summary>
        /// Creates default balanced options using the recommended model
        /// </summary>
        public static GeminiRequestOptions Default(string? model = null) => new()
        {
            Temperature = GeminiConstants.Defaults.Temperature,
            Model = model ?? GeminiConstants.Models.Recommended
        };

        /// <summary>
        /// Creates options optimized for fast responses using the fastest model
        /// </summary>
        public static GeminiRequestOptions Fast() => new()
        {
            Temperature = GeminiConstants.Defaults.Temperature,
            Model = GeminiConstants.Models.Fastest
        };

        /// <summary>
        /// Creates options with a specific model
        /// </summary>
        /// <param name="model">The specific model to use</param>
        /// <param name="temperature">Optional temperature setting</param>
        public static GeminiRequestOptions WithModel(string model, float? temperature = 0.7f) => new()
        {
            Model = model,
            Temperature = temperature
        };

        /// <summary>
        /// Creates a copy of current options with a different model
        /// </summary>
        /// <param name="model">The model to use</param>
        public GeminiRequestOptions UseModel(string model)
        {
            var copy = new GeminiRequestOptions
            {
                Temperature = this.Temperature,
                MaxTokens = this.MaxTokens,
                TopP = this.TopP,
                TopK = this.TopK,
                Model = model,
                SafetySettings = this.SafetySettings,
                StopSequences = this.StopSequences
            };
            return copy;
        }
    }
}