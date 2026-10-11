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

    /// <summary>
    /// Code the model generated and ran (only present when the <c>code_execution</c> tool is enabled,
    /// e.g. via <see cref="Requests.GeminiRequestOptions.EnableCodeExecution"/>). Paired with a
    /// following <see cref="CodeExecutionResult"/> part carrying its output.
    /// </summary>
    [JsonPropertyName("executableCode")]
    public ExecutableCode? ExecutableCode { get; set; }

    /// <summary>
    /// The output of a preceding <see cref="ExecutableCode"/> part (only present when the
    /// <c>code_execution</c> tool is enabled).
    /// </summary>
    [JsonPropertyName("codeExecutionResult")]
    public CodeExecutionResult? CodeExecutionResult { get; set; }

    /// <summary>True when this part is a thought summary (only present when thoughts are included).</summary>
    [JsonPropertyName("thought")]
    public bool? Thought { get; set; }

    /// <summary>
    /// Encrypted "thought signature" (Gemini 3+). The API returns it on response parts and requires
    /// it to be echoed back in follow-up turns. For function calling, a missing signature causes a
    /// 400. Preserve the response parts (and this value) when building multi-turn history.
    /// </summary>
    [JsonPropertyName("thoughtSignature")]
    public string? ThoughtSignature { get; set; }

    /// <summary>
    /// Speaker and delivery style for a text part sent to a Gemini 3.8 TTS model. The 3.8 models read
    /// the text as a verbatim transcript, so this is where a speaker name and a style such as
    /// "whispering" go. The older TTS models reject this field with HTTP 400. The library sets it for
    /// you when you use <c>GeminiRequestOptions.SpeakerVoices</c> or <c>SpeechStyle</c>.
    /// </summary>
    [JsonPropertyName("speechMetadata")]
    public SpeechMetadata? SpeechMetadata { get; set; }

    /// <summary>
    /// How the model reads a video part: one of the <c>GeminiConstants.MediaProcessing</c> values.
    /// Google ignores it on parts that are not video.
    /// <para>
    /// Live-tested on 2026-10-10 against <c>gemini-3.8-flash</c> with two clips of about 10 seconds.
    /// Leaving it unset and <c>STATIC</c> behaved the same (671 prompt tokens). <c>AGENTIC</c> was
    /// accepted and answered correctly. Its response also carries extra <c>toolCall</c> and
    /// <c>toolResponse</c> parts, which this library does not model. <c>GenerateContentResponse.Text()</c>
    /// still returns the answer. The video tokens move out of <see cref="UsageMetadata.PromptTokenCount"/>
    /// into <see cref="UsageMetadata.ToolUsePromptTokenCount"/>. On these short clips <c>AGENTIC</c> used
    /// slightly more input tokens (723 against 671). Google says it uses up to 88% fewer on long videos.
    /// We have not tested that.
    /// </para>
    /// <para>
    /// Use the Files API for a large video. Live-verified on 2026-10-10: a file uploaded through the Files
    /// API works in agentic mode with or without <see cref="FileData.MimeType"/>. We could not get a clear
    /// rule for an external URL. A public YouTube URL returned HTTP 403 on our project. Without a
    /// <see cref="FileData.MimeType"/> it returned HTTP 400 ("mime_type must be set when media_processing
    /// is specified"), so set it for any URL.
    /// </para>
    /// </summary>
    [JsonPropertyName("mediaProcessing")]
    public string? MediaProcessing { get; set; }
}

/// <summary>
/// Speaker and style for one text part, for Gemini 3.8 TTS models (<c>Part.speechMetadata</c>).
/// Live-verified on 2026-10-10, see <c>PLAN-tts-3-8.md</c>.
/// </summary>
public class SpeechMetadata
{
    /// <summary>
    /// The speaker for this turn. In a multi-speaker request it must match a name in
    /// <c>SpeakerVoices</c>, and every text part must set it.
    /// </summary>
    [JsonPropertyName("speaker")]
    public string? Speaker { get; set; }

    /// <summary>A short delivery direction for this turn, for example "whispering" or "cheerful".</summary>
    [JsonPropertyName("style")]
    public string? Style { get; set; }
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
    /// <summary>
    /// The call identifier the model assigned (for example <c>"call_148005"</c>). Gemini 3 models
    /// return it. Copy it into <see cref="FunctionResponsePart.Id"/> when you reply. A reply without it
    /// still worked on <c>gemini-3.8-flash</c> in a live test (2026-10-10), but Google's 3.8 migration
    /// checklist asks for it, and it is the only way to match replies to parallel calls of one function.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

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
    /// <summary>
    /// The <see cref="FunctionCallPart.Id"/> of the call this response answers. Leave it null when the
    /// call had no id.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>The function name being responded to.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The function result, as a JSON object.</summary>
    [JsonPropertyName("response")]
    public JsonNode? Response { get; set; }
}

/// <summary>Code the model wrote and ran via the <c>code_execution</c> tool.</summary>
public class ExecutableCode
{
    /// <summary>The programming language of <see cref="Code"/> (e.g. <c>"PYTHON"</c>).</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>The source code that was run.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

/// <summary>The result of running a preceding <see cref="ExecutableCode"/> part.</summary>
public class CodeExecutionResult
{
    /// <summary>Whether execution succeeded (e.g. <c>"OUTCOME_OK"</c>, <c>"OUTCOME_FAILED"</c>).</summary>
    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    /// <summary>Stdout/stderr captured from running the code.</summary>
    [JsonPropertyName("output")]
    public string? Output { get; set; }
}
