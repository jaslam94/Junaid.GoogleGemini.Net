using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using System.Text.Json.Nodes;

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
        /// System-level instruction applied to the request (persona, tone, rules). Text only.
        /// </summary>
        public string? SystemInstruction { get; set; }

        /// <summary>
        /// Output MIME type (e.g. <c>application/json</c> for structured output). When using
        /// <c>GenerateAsync&lt;T&gt;</c> this is set for you.
        /// </summary>
        public string? ResponseMimeType { get; set; }

        /// <summary>A response schema (OpenAPI subset) the JSON output must conform to.</summary>
        public JsonNode? ResponseSchema { get; set; }

        /// <summary>
        /// Thinking/reasoning token budget (Gemini 2.5+). <c>0</c> disables thinking where allowed;
        /// <c>-1</c> lets the model decide.
        /// </summary>
        public int? ThinkingBudget { get; set; }

        /// <summary>When true, includes thought-summary parts in the response.</summary>
        public bool? IncludeThoughts { get; set; }

        /// <summary>Deterministic sampling seed.</summary>
        public int? Seed { get; set; }

        /// <summary>Number of candidate responses to generate.</summary>
        public int? CandidateCount { get; set; }

        /// <summary>
        /// Creates options optimized for creative tasks using the recommended model
        /// </summary>
        public static GeminiRequestOptions Creative(string? model = null) => new()
        {
            Temperature = 0.9f,
            TopP = 0.8f,
            TopK = 40,
            Model = model ?? GeminiConstants.Models.Recommended,
            SafetySettings = new List<SafetySetting>
            {
                new() { Category = GeminiConstants.SafetyCategories.Harassment, Threshold = GeminiConstants.SafetyThresholds.High },
                new() { Category = GeminiConstants.SafetyCategories.HateSpeech, Threshold = GeminiConstants.SafetyThresholds.High },
                new() { Category = GeminiConstants.SafetyCategories.SexuallyExplicit, Threshold = GeminiConstants.SafetyThresholds.High },
                new() { Category = GeminiConstants.SafetyCategories.DangerousContent, Threshold = GeminiConstants.SafetyThresholds.High }
            }
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
            var copy = Clone();
            copy.Model = model;
            return copy;
        }

        /// <summary>Creates a shallow copy of these options (used to avoid mutating a caller's instance).</summary>
        public GeminiRequestOptions Clone() => new()
        {
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            TopP = TopP,
            TopK = TopK,
            Model = Model,
            SafetySettings = SafetySettings,
            StopSequences = StopSequences,
            SystemInstruction = SystemInstruction,
            ResponseMimeType = ResponseMimeType,
            ResponseSchema = ResponseSchema,
            ThinkingBudget = ThinkingBudget,
            IncludeThoughts = IncludeThoughts,
            Seed = Seed,
            CandidateCount = CandidateCount
        };
    }
}