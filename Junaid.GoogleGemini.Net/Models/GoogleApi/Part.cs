using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// A single piece of content: text, inline binary data, a function call/result, etc.
/// </summary>
public class Part
{
    /// <summary>Text content.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Inline binary data (e.g. an image), base64-encoded.</summary>
    [JsonPropertyName("inlineData")]
    public InlineData? InlineData { get; set; }

    /// <summary>A reference to a file uploaded via the Files API.</summary>
    [JsonPropertyName("fileData")]
    public FileData? FileData { get; set; }

    /// <summary>A function call requested by the model.</summary>
    [JsonPropertyName("functionCall")]
    public FunctionCallPart? FunctionCall { get; set; }

    /// <summary>A function result you send back to the model.</summary>
    [JsonPropertyName("functionResponse")]
    public FunctionResponsePart? FunctionResponse { get; set; }

    /// <summary>True when this part is a thought summary (only present when thoughts are included).</summary>
    [JsonPropertyName("thought")]
    public bool? Thought { get; set; }
}

/// <summary>References a file uploaded via the Files API.</summary>
public class FileData
{
    /// <summary>The file's MIME type.</summary>
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    /// <summary>The file URI returned by the upload (<see cref="FileResource.Uri"/>).</summary>
    [JsonPropertyName("fileUri")]
    public string? FileUri { get; set; }
}

/// <summary>A function call emitted by the model.</summary>
public class FunctionCallPart
{
    /// <summary>The function name the model wants to call.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The arguments, as a JSON object.</summary>
    [JsonPropertyName("args")]
    public JsonNode? Args { get; set; }
}

/// <summary>A function result sent back to the model.</summary>
public class FunctionResponsePart
{
    /// <summary>The function name being responded to.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The function result, as a JSON object.</summary>
    [JsonPropertyName("response")]
    public JsonNode? Response { get; set; }
}
