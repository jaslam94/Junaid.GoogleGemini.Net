namespace Junaid.GoogleGemini.Net.Exceptions
{
    /// <summary>
    /// Thrown when a request is rejected by the <i>client-side</i> rate limiter before it is sent.
    /// (A server-side 429 surfaces as a <see cref="GeminiApiException"/> with that status code.)
    /// </summary>
    public class GeminiRateLimitException : GeminiException
    {
        /// <summary>Suggested time to wait before retrying, if known.</summary>
        public TimeSpan? RetryAfter { get; }

        /// <summary>Creates a new <see cref="GeminiRateLimitException"/>.</summary>
        public GeminiRateLimitException(string message, TimeSpan? retryAfter = null) : base(message)
        {
            RetryAfter = retryAfter;
        }
    }

    /// <summary>Thrown when a request exceeds the configured timeout.</summary>
    public class GeminiTimeoutException : GeminiException
    {
        /// <summary>Creates a new <see cref="GeminiTimeoutException"/>.</summary>
        public GeminiTimeoutException(string message) : base(message)
        {
        }

        /// <summary>Creates a new <see cref="GeminiTimeoutException"/> wrapping an underlying cause.</summary>
        public GeminiTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>Thrown when a request or response payload cannot be serialized or parsed.</summary>
    public class GeminiSerializationException : GeminiException
    {
        /// <summary>Creates a new <see cref="GeminiSerializationException"/>.</summary>
        public GeminiSerializationException(string message) : base(message)
        {
        }

        /// <summary>Creates a new <see cref="GeminiSerializationException"/> wrapping an underlying cause.</summary>
        public GeminiSerializationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown by <see cref="Models.GoogleApi.GenerateContentResponse.GetTextOrThrow"/> when a response
    /// carries no usable text — e.g. it was blocked by safety filters or truncated. The
    /// <see cref="FinishReason"/> and <see cref="BlockReason"/> explain why.
    /// </summary>
    public class GeminiContentException : GeminiException
    {
        /// <summary>Why generation finished (e.g. "SAFETY", "MAX_TOKENS"), if available.</summary>
        public string? FinishReason { get; }

        /// <summary>Why the prompt was blocked, if applicable.</summary>
        public string? BlockReason { get; }

        /// <summary>Creates a new <see cref="GeminiContentException"/>.</summary>
        public GeminiContentException(string message, string? finishReason = null, string? blockReason = null)
            : base(message)
        {
            FinishReason = finishReason;
            BlockReason = blockReason;
        }
    }
}
