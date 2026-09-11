namespace Junaid.GoogleGemini.Net.Models.Requests;

/// <summary>
/// One input for a multimodal batch embedding call
/// (<see cref="Junaid.GoogleGemini.Net.Services.Interfaces.IEmbeddingService"/>'s
/// <c>BatchEmbedContentAsync</c> overload taking a list of these). At least one of <see cref="Text"/>
/// or (<see cref="MediaBytes"/>/<see cref="FileUri"/>) should be set. Set at most one of
/// <see cref="MediaBytes"/>/<see cref="FileUri"/>, not both.
/// </summary>
public class EmbeddingInput
{
    /// <summary>Optional text instruction/content, combined with any media into one aggregated embedding.</summary>
    public string? Text { get; set; }

    /// <summary>Inline media bytes (image, audio, video, or PDF). Requires <see cref="MimeType"/>.</summary>
    public byte[]? MediaBytes { get; set; }

    /// <summary>A Files API URI (from <see cref="Junaid.GoogleGemini.Net.Services.Interfaces.IFileService.UploadFileAsync"/>), for large media. Requires <see cref="MimeType"/>.</summary>
    public string? FileUri { get; set; }

    /// <summary>The media's MIME type, e.g. <c>"image/jpeg"</c>, <c>"audio/wav"</c>. Required with <see cref="MediaBytes"/> or <see cref="FileUri"/>.</summary>
    public string? MimeType { get; set; }
}
