using System.Net;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Exceptions
{
    /// <summary>
    /// Thrown when the Gemini API returns a non-success HTTP response. Carries the HTTP status code
    /// and the parsed error detail so callers can branch on what actually went wrong.
    /// </summary>
    public class GeminiApiException : GeminiException
    {
        /// <summary>The HTTP status code returned by the API.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>The parsed error detail, if the body contained one.</summary>
        public ApiError? Error { get; }

        /// <summary>The machine-readable status string (e.g. "INVALID_ARGUMENT"), if available.</summary>
        public string? Status => Error?.Status;

        /// <summary>The numeric error code, if available.</summary>
        public int? ErrorCode => Error?.Code;

        /// <summary>
        /// The Interactions API's machine-readable error code (e.g. <c>"invalid_request"</c>,
        /// <c>"not_found"</c>), if this exception came from that API (see
        /// <see cref="Junaid.GoogleGemini.Net.Services.Interfaces.ITranscriptionService"/>). Null for
        /// every other Gemini call; use <see cref="Status"/> for those instead. Kept as a separate
        /// field rather than forced into <see cref="ApiError"/> (whose <c>Code</c> is numeric, not a
        /// string) so <see cref="ApiError"/> keeps meaning exactly what it already means for
        /// <c>generateContent</c>. See <c>PLAN-stt.md</c> §3.4/§5.2.
        /// </summary>
        public string? InteractionErrorCode { get; }

        /// <summary>Creates a new <see cref="GeminiApiException"/>.</summary>
        public GeminiApiException(string message, HttpStatusCode statusCode, ApiError? error = null)
            : base(message)
        {
            StatusCode = statusCode;
            Error = error;
        }

        /// <summary>Creates a new <see cref="GeminiApiException"/> from an Interactions API error.</summary>
        public GeminiApiException(string message, HttpStatusCode statusCode, string? interactionErrorCode)
            : base(message)
        {
            StatusCode = statusCode;
            InteractionErrorCode = interactionErrorCode;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var status = $" (HTTP {(int)StatusCode} {StatusCode})";
            var detail = Error?.Status is { Length: > 0 } s
                ? $" [{s}]"
                : InteractionErrorCode is { Length: > 0 } ic ? $" [{ic}]" : string.Empty;
            return $"{base.ToString()}{status}{detail}";
        }
    }
}
