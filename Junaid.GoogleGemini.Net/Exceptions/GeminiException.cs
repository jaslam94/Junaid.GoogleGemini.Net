namespace Junaid.GoogleGemini.Net.Exceptions
{
    /// <summary>
    /// Base type for all errors raised by this library. Catch <see cref="GeminiException"/> to handle
    /// any Gemini-related failure, or catch a more specific derived type
    /// (<see cref="GeminiApiException"/>, <see cref="GeminiRateLimitException"/>,
    /// <see cref="GeminiTimeoutException"/>, <see cref="GeminiSerializationException"/>,
    /// <see cref="GeminiContentException"/>) to react to a particular failure mode.
    /// </summary>
    /// <remarks>
    /// Why a hierarchy instead of one exception type? It lets callers write intent-revealing
    /// <c>catch</c> blocks (e.g. back off on <see cref="GeminiRateLimitException"/>, surface a
    /// validation message on <see cref="GeminiApiException"/>) without string-matching messages.
    /// </remarks>
    public class GeminiException : Exception
    {
        /// <summary>Creates a new <see cref="GeminiException"/>.</summary>
        public GeminiException(string message) : base(message)
        {
        }

        /// <summary>Creates a new <see cref="GeminiException"/> wrapping an underlying cause.</summary>
        public GeminiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
