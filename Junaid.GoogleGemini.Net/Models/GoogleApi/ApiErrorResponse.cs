using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>The error envelope returned by the Gemini API on failure.</summary>
public class ApiErrorResponse
{
    /// <summary>The error detail.</summary>
    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }
}

/// <summary>Details of a Gemini API error.</summary>
public class ApiError
{
    /// <summary>HTTP-style numeric error code.</summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Machine-readable status string (e.g. "INVALID_ARGUMENT").</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
