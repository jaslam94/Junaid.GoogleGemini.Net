using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.Functions
{
    /// <summary>
    /// Represents a function that can be called by the Gemini model
    /// </summary>
    public class FunctionDefinition
    {
        /// <summary>
        /// The name of the function
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A description of what the function does
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The parameters that the function accepts
        /// </summary>
        [JsonPropertyName("parameters")]
        public FunctionParameters Parameters { get; set; } = new();

        /// <summary>
        /// Whether the function is required to be called
        /// </summary>
        [JsonPropertyName("required")]
        public bool Required { get; set; }
    }

    /// <summary>
    /// Represents the parameters of a function
    /// </summary>
    public class FunctionParameters
    {
        /// <summary>
        /// The type of the parameters object (usually "object")
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        /// <summary>
        /// The properties that make up the parameters
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, PropertyDefinition> Properties { get; set; } = new();

        /// <summary>
        /// The names of required parameters
        /// </summary>
        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new();
    }

    /// <summary>
    /// Represents a property in a function's parameters
    /// </summary>
    public class PropertyDefinition
    {
        /// <summary>
        /// The type of the property (string, number, boolean, array, etc.)
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// A description of what the property is used for
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// For array types, the type of items in the array
        /// </summary>
        [JsonPropertyName("items")]
        public PropertyDefinition? Items { get; set; }

        /// <summary>
        /// For enum types, the possible values
        /// </summary>
        [JsonPropertyName("enum")]
        public List<string>? Enum { get; set; }
    }
}