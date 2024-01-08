using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Exceptions
{
    public class GeminiException : Exception
    {
        public ApiErrorResponse? ErrorResponse { get; set; }

        public GeminiException(string message) : base(message)
        {
        }

        public GeminiException(string message, Exception err) : base(message, err)
        {
        }

        public GeminiException(ApiErrorResponse geminiError, string message) : base(message)
        {
            ErrorResponse = geminiError;
        }
    }
}