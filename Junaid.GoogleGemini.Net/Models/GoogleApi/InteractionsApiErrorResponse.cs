using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// The error envelope the Interactions API returns on failure. NOT the same shape as
/// <see cref="ApiErrorResponse"/>: <c>generateContent</c>'s <c>error.code</c> is a numeric HTTP-style
/// code (e.g. <c>404</c>); the Interactions API's <c>error.code</c> is a string enum (e.g.
/// <c>"invalid_request"</c>, <c>"not_found"</c>). Confirmed live, see <c>PLAN-stt.md</c> §3.4.
/// Deserializing an Interactions error body with <see cref="ApiErrorResponse"/> throws a
/// <see cref="System.Text.Json.JsonException"/> on that type mismatch; use this type instead.
/// </summary>
public class InteractionsApiErrorResponse
{
    [JsonPropertyName("error")]
    public InteractionsApiError? Error { get; set; }
}

/// <summary>Error detail from the Interactions API. See <see cref="Exceptions.GeminiApiException.InteractionErrorCode"/>.</summary>
public class InteractionsApiError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>A string status, e.g. <c>"invalid_request"</c>, <c>"not_found"</c>. Unlike
    /// <c>generateContent</c>'s numeric <see cref="ApiError.Code"/>.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
