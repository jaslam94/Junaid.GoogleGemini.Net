using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// A file stored via the Gemini Files API. Reference it in a request through a <see cref="FileData"/>
/// part using <see cref="Uri"/>. Large media (video/audio) must reach <c>ACTIVE</c> state before use.
/// </summary>
public class FileResource
{
    /// <summary>Resource name, e.g. <c>files/abc123</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Human-readable display name.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>The file's MIME type.</summary>
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    /// <summary>Size in bytes (returned as a string by the API).</summary>
    [JsonPropertyName("sizeBytes")]
    public string? SizeBytes { get; set; }

    /// <summary>Creation timestamp (RFC3339).</summary>
    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }

    /// <summary>Expiration timestamp (RFC3339); files are retained ~48h.</summary>
    [JsonPropertyName("expirationTime")]
    public string? ExpirationTime { get; set; }

    /// <summary>Processing state: <c>PROCESSING</c>, <c>ACTIVE</c>, or <c>FAILED</c>.</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>The URI to reference this file in a request (via a <see cref="FileData"/> part).</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}

/// <summary>Wrapper returned by the upload finalize step (<c>{ "file": { ... } }</c>).</summary>
public class FileUploadResponse
{
    /// <summary>The uploaded file.</summary>
    [JsonPropertyName("file")]
    public FileResource? File { get; set; }
}

/// <summary>Response for listing files.</summary>
public class FileListResponse
{
    /// <summary>The files.</summary>
    [JsonPropertyName("files")]
    public FileResource[]? Files { get; set; }

    /// <summary>Token for the next page, if any.</summary>
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>Body for starting a resumable upload (<c>{ "file": { "display_name": ... } }</c>).</summary>
public class FileUploadStartRequest
{
    /// <summary>The file metadata.</summary>
    [JsonPropertyName("file")]
    public FileUploadMetadata File { get; set; } = new();
}

/// <summary>Metadata supplied when starting an upload.</summary>
public class FileUploadMetadata
{
    /// <summary>Optional display name for the uploaded file.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
