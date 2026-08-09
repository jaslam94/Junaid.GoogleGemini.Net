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

    /// <summary>
    /// Thrown by <see cref="Infrastructure.ICostGovernor.CheckBudget"/> before a request is sent, when
    /// today's (UTC) cumulative spend has already reached the configured
    /// <see cref="Infrastructure.Options.BudgetOptions.MaxCostPerDayUsd"/> ceiling. The rejected call
    /// itself never reaches the network, so it costs nothing.
    /// </summary>
    public class GeminiBudgetExceededException : GeminiException
    {
        /// <summary>Today's (UTC) cumulative spend at the moment this call was rejected, in USD.</summary>
        public decimal CurrentSpendUsd { get; }

        /// <summary>The configured daily ceiling that was reached, in USD.</summary>
        public decimal BudgetLimitUsd { get; }

        /// <summary>Creates a new <see cref="GeminiBudgetExceededException"/>.</summary>
        public GeminiBudgetExceededException(string message, decimal currentSpendUsd, decimal budgetLimitUsd)
            : base(message)
        {
            CurrentSpendUsd = currentSpendUsd;
            BudgetLimitUsd = budgetLimitUsd;
        }
    }

    /// <summary>
    /// Thrown by <see cref="Infrastructure.ICostGovernor.CheckEstimatedRequestCost"/> before a request
    /// is sent, when its best-effort <b>estimated</b> cost exceeds the configured
    /// <see cref="Infrastructure.Options.BudgetOptions.MaxCostPerRequestUsd"/> ceiling.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GeminiBudgetExceededException"/> (built from real, already-billed usage), this
    /// is an <b>estimate</b>: input cost is exact (from <c>CountTokensAsync</c>), but output cost is
    /// only bounded when the request set <c>GeminiRequestOptions.MaxTokens</c> — see the XML docs on
    /// <see cref="Infrastructure.Options.BudgetOptions.MaxCostPerRequestUsd"/> for exactly what this
    /// can and cannot guarantee.
    /// </remarks>
    public class GeminiRequestCostExceededException : GeminiException
    {
        /// <summary>The estimated cost of the request that was rejected, in USD.</summary>
        public decimal EstimatedCostUsd { get; }

        /// <summary>The configured per-request ceiling that was exceeded, in USD.</summary>
        public decimal MaxCostPerRequestUsd { get; }

        /// <summary>Creates a new <see cref="GeminiRequestCostExceededException"/>.</summary>
        public GeminiRequestCostExceededException(string message, decimal estimatedCostUsd, decimal maxCostPerRequestUsd)
            : base(message)
        {
            EstimatedCostUsd = estimatedCostUsd;
            MaxCostPerRequestUsd = maxCostPerRequestUsd;
        }
    }
}
