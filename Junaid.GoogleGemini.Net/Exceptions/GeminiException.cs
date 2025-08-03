using Junaid.GoogleGemini.Net.Models.GoogleApi;
using System.Net;

namespace Junaid.GoogleGemini.Net.Exceptions
{
    /// <summary>
    /// Represents errors that occur during interaction with the Gemini API
    /// </summary>
    public class GeminiException : Exception
    {
        /// <summary>
        /// The detailed error response from the Gemini API, if available
        /// </summary>
        public ApiErrorResponse? ErrorResponse { get; set; }

        /// <summary>
        /// The HTTP status code of the failed request, if available
        /// </summary>
        public HttpStatusCode? StatusCode { get; set; }

        /// <summary>
        /// Creates a new instance of GeminiException
        /// </summary>
        /// <param name="message">The error message</param>
        public GeminiException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates a new instance of GeminiException with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception that caused this error</param>
        public GeminiException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a new instance of GeminiException with the API error response
        /// </summary>
        /// <param name="geminiError">The API error response</param>
        /// <param name="message">The error message</param>
        public GeminiException(ApiErrorResponse geminiError, string message) : base(message)
        {
            ErrorResponse = geminiError;
        }

        /// <summary>
        /// Returns a string representation of the exception including the error details
        /// </summary>
        public override string ToString()
        {
            var status = StatusCode.HasValue ? $" (Status: {StatusCode})" : string.Empty;
            var errorDetails = ErrorResponse?.error?.message != null ? $"\nAPI Error: {ErrorResponse.error.message}" : string.Empty;
            return $"{base.ToString()}{status}{errorDetails}";
        }
    }
}