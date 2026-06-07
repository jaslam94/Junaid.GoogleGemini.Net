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

        /// <summary>Creates a new <see cref="GeminiApiException"/>.</summary>
        public GeminiApiException(string message, HttpStatusCode statusCode, ApiError? error = null)
            : base(message)
        {
            StatusCode = statusCode;
            Error = error;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var status = $" (HTTP {(int)StatusCode} {StatusCode})";
            var detail = Error?.Status is { Length: > 0 } s ? $" [{s}]" : string.Empty;
            return $"{base.ToString()}{status}{detail}";
        }
    }
}
