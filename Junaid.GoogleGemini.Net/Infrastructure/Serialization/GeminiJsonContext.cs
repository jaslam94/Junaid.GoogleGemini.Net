using System.Text.Json.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Infrastructure.Serialization;

/// <summary>
/// System.Text.Json <b>source-generation</b> context for the Gemini wire models.
///
/// Why source generation? The compiler emits the (de)serialization metadata at build time instead
/// of the library discovering it via reflection at runtime. That means:
///   • faster startup and lower allocations (no reflection metadata to build), and
///   • Native-AOT / trimming friendliness (the trimmer can see exactly which members are used),
///     which is a deliberate step toward the AOT goal on the roadmap.
///
/// Every root type the client serializes or deserializes is registered below; nested types
/// (Content, Part, Candidate, …) are discovered automatically.
///
/// The serialization options here are kept in lock-step with <see cref="GeminiJson.Default"/> so a
/// type produces identical JSON whether it goes through the source-generated fast path or the
/// reflection fallback.
/// </summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GenerateContentRequest))]
[JsonSerializable(typeof(GenerateContentResponse))]
[JsonSerializable(typeof(CountTokensRequest))]
[JsonSerializable(typeof(CountTokensResponse))]
[JsonSerializable(typeof(EmbedContentRequest))]
[JsonSerializable(typeof(BatchEmbedContentRequest))]
[JsonSerializable(typeof(SingleEmbedContentRequest))]
[JsonSerializable(typeof(EmbedContentResponse))]
[JsonSerializable(typeof(BatchEmbedContentResponse))]
[JsonSerializable(typeof(ListModelsResponse))]
[JsonSerializable(typeof(ListModelInfoResponse))]
[JsonSerializable(typeof(ModelInfo))]
[JsonSerializable(typeof(ApiErrorResponse))]
[JsonSerializable(typeof(FileResource))]
[JsonSerializable(typeof(FileUploadResponse))]
[JsonSerializable(typeof(FileListResponse))]
[JsonSerializable(typeof(FileUploadStartRequest))]
[JsonSerializable(typeof(CachedContent))]
[JsonSerializable(typeof(CachedContentList))]
[JsonSerializable(typeof(CreateBatchRequest))]
[JsonSerializable(typeof(BatchJob))]
[JsonSerializable(typeof(BatchJobList))]
[JsonSerializable(typeof(BatchRequestLine))]
[JsonSerializable(typeof(InlinedBatchResponse))]
internal partial class GeminiJsonContext : JsonSerializerContext
{
}
