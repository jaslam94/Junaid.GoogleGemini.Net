using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Models
{
    public class GeminiException : Exception
    {
        public ErrorResponse ErrorResponse { get; set; }

        public GeminiException()
        {
        }

        public GeminiException(string message) : base(message)
        {
        }

        public GeminiException(string message, Exception err) : base(message, err)
        {
        }

        public GeminiException(ErrorResponse geminiError, string message) : base(message)
        {
            ErrorResponse = geminiError;
        }
    }
}