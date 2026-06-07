using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// A tool the model may use. A single tool may carry function declarations and/or enable one of the
/// built-in tools (Google Search grounding, URL context, code execution).
/// </summary>
public class Tool
{
    /// <summary>Functions the model may call.</summary>
    [JsonPropertyName("functionDeclarations")]
    public List<FunctionDeclaration>? FunctionDeclarations { get; set; }

    /// <summary>Enables grounding with Google Search when set to an (empty) instance.</summary>
    [JsonPropertyName("googleSearch")]
    public GoogleSearchTool? GoogleSearch { get; set; }

    /// <summary>Enables the URL-context tool when set to an (empty) instance.</summary>
    [JsonPropertyName("urlContext")]
    public UrlContextTool? UrlContext { get; set; }

    /// <summary>Enables server-side code execution when set to an (empty) instance.</summary>
    [JsonPropertyName("codeExecution")]
    public CodeExecutionTool? CodeExecution { get; set; }
}

/// <summary>Marker enabling grounding with Google Search.</summary>
public class GoogleSearchTool { }

/// <summary>Marker enabling the URL-context tool.</summary>
public class UrlContextTool { }

/// <summary>Marker enabling server-side code execution.</summary>
public class CodeExecutionTool { }

/// <summary>Declares a function the model may call.</summary>
public class FunctionDeclaration
{
    /// <summary>The function name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>What the function does (helps the model decide when to call it).</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Parameter schema (OpenAPI subset).</summary>
    [JsonPropertyName("parameters")]
    public JsonNode? Parameters { get; set; }
}

/// <summary>Controls how the model is allowed to call functions.</summary>
public class ToolConfig
{
    /// <summary>Function-calling configuration.</summary>
    [JsonPropertyName("functionCallingConfig")]
    public FunctionCallingConfig? FunctionCallingConfig { get; set; }
}

/// <summary>Function-calling mode and allow-list.</summary>
public class FunctionCallingConfig
{
    /// <summary>One of "AUTO", "ANY", or "NONE".</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>When mode is "ANY", restricts calls to these function names.</summary>
    [JsonPropertyName("allowedFunctionNames")]
    public List<string>? AllowedFunctionNames { get; set; }
}
