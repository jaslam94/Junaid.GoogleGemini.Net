using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.Functions
{
    /// <summary>
    /// Represents a function call from the Gemini model
    /// </summary>
    public class FunctionCall
    {
        /// <summary>
        /// The name of the function to call
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The arguments to pass to the function, serialized as a JSON object
        /// </summary>
        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the result of a function call
    /// </summary>
    public class FunctionResult
    {
        /// <summary>
        /// The name of the function that was called
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The result of the function call, serialized as a JSON string
        /// </summary>
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;

        /// <summary>
        /// Any error that occurred during the function call
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}