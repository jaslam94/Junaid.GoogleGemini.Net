namespace Junaid.GoogleGemini.Net.Infrastructure
{
    /// <summary>Named <see cref="HttpClient"/> keys used by the library's DI registration.</summary>
    public static class GeminiHttpClients
    {
        /// <summary>
        /// Client for the Files API. It targets the host root (not the versioned base address) because
        /// uploads use the <c>/upload</c> path and a server-issued absolute session URL.
        /// </summary>
        public const string Files = "GeminiFiles";

        /// <summary>
        /// Client for the Batch API. Targets the same versioned base address as the main
        /// <c>GeminiClient</c>, but is deliberately its own <c>HttpClient</c> rather than going through
        /// <c>IGeminiClient</c>: <c>GeminiClient</c>'s PostAsync/GetAsync/DeleteAsync unconditionally
        /// run every call through the interactive rate limiter and call
        /// <c>ICostGovernor.CheckBudget</c>, both of which are scoped to interactive per-minute-RPM and
        /// per-day-USD budgets. Batch jobs draw from a wholly separate quota pool at a different
        /// (discounted) price, so routing them through the interactive gates would be actively wrong.
        /// A user near their daily interactive budget would see batch job creation mysteriously
        /// rejected by a budget it isn't even priced against. This client still gets the auth handler
        /// and retry/resilience handling, just not those two gates.
        /// </summary>
        public const string Batches = "GeminiBatches";
    }
}
