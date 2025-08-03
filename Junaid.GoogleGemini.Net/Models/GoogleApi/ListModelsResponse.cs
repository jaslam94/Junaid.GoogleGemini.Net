using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    /// <summary>
    /// Response containing a list of available models
    /// </summary>
    public class ListModelsResponse
    {
        /// <summary>
        /// Array of available models
        /// </summary>
        [JsonPropertyName("models")]
        public ModelInfo[] models { get; set; } = Array.Empty<ModelInfo>();
    }
}