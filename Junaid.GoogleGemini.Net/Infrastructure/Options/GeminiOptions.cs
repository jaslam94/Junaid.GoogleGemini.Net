using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using System.ComponentModel.DataAnnotations;

namespace Junaid.GoogleGemini.Net.Infrastructure.Options
{
    /// <summary>
    /// Configuration options for the Gemini API client
    /// </summary>
    public class GeminiOptions
    {
        /// <summary>
        /// Base URL for the Gemini API
        /// </summary>
        [Required]
        public Uri BaseUrl { get; set; } = new Uri(GeminiConstants.DefaultBaseUrl);

        /// <summary>
        /// API key for authentication
        /// Can be set via configuration or environment variable 'GeminiApiKey'
        /// </summary>
        [Required]
        public string ApiKey { get; set; } = GetApiKeyFromEnvironment();

        /// <summary>
        /// Default timeout for API requests in seconds
        /// </summary>
        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = GeminiConstants.Defaults.TimeoutSeconds;

        /// <summary>
        /// Maximum number of retries for transient failures (HTTP 429, 5xx, network errors).
        /// </summary>
        [Range(0, 5)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Base delay for the exponential backoff between retries. Lower it (e.g. to near-zero) in
        /// tests to keep them fast.
        /// </summary>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Default model to use for requests
        /// </summary>
        public string DefaultModel { get; set; } = GeminiConstants.Defaults.Model;

        /// <summary>
        /// Rate limiting settings
        /// </summary>
        public RateLimitOptions RateLimit { get; set; } = new();

        /// <summary>
        /// Proxy settings
        /// </summary>
        public ProxyOptions? Proxy { get; set; }

        /// <summary>Cost governance settings. Null (the default) disables the feature entirely — zero overhead.</summary>
        public BudgetOptions? Budget { get; set; }

        /// <summary>
        /// Gets API key from environment variable if not set in configuration
        /// </summary>
        private static string GetApiKeyFromEnvironment()
        {
            return Environment.GetEnvironmentVariable("GeminiApiKey") ?? string.Empty;
        }
    }

    /// <summary>
    /// Rate limiting configuration options
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Maximum number of requests per minute
        /// </summary>
        [Range(1, 1000)]
        public int RequestsPerMinute { get; set; } = 60;

        /// <summary>
        /// Maximum number of tokens per minute
        /// </summary>
        [Range(1, 1000000)]
        public int TokensPerMinute { get; set; } = 60000;

        /// <summary>
        /// Whether to enable rate limiting
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>Cost governance configuration.</summary>
    public class BudgetOptions
    {
        /// <summary>Master switch. Defaults true; set false to keep the section configured but inert
        /// (e.g. to toggle per-environment without removing config).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Cumulative UTC-calendar-day USD ceiling. Once today's real recorded spend reaches this, the
        /// next call throws <see cref="Junaid.GoogleGemini.Net.Exceptions.GeminiBudgetExceededException"/>
        /// before being sent. Null = no daily ceiling (cost is still tracked/recorded, just not enforced).
        /// </summary>
        public decimal? MaxCostPerDayUsd { get; set; }

        /// <summary>
        /// Optional, best-effort single-request estimate ceiling, checked BEFORE the request is sent
        /// (throws <see cref="Junaid.GoogleGemini.Net.Exceptions.GeminiRequestCostExceededException"/>
        /// if exceeded). Null (default) = not enforced, and — importantly — skipped entirely at zero
        /// cost: when unset, no extra <c>CountTokensAsync</c> round-trip is made.
        /// </summary>
        /// <remarks>
        /// This is a genuine estimate, not an exact figure — read this before relying on it:
        /// <list type="bullet">
        /// <item><description>
        /// <b>Input cost is exact</b> (a real <c>CountTokensAsync(prompt, options, ct)</c> call happens
        /// first), except it does not include <c>SystemInstruction</c>/<c>Tools</c>/<c>CachedContent</c>
        /// tokens — the token-counting endpoint's request shape omits them — so it can undercount input
        /// for calls that use those. It also always prices the full input at the standard (non-cached)
        /// rate, since it can't know ahead of time how much Gemini will actually serve from cache, which
        /// makes it conservative (an over-estimate, never an under-estimate) on that axis.
        /// </description></item>
        /// <item><description>
        /// <b>Output cost is only bounded when the request sets
        /// <see cref="Junaid.GoogleGemini.Net.Models.Requests.GeminiRequestOptions.MaxTokens"/></b> (widened
        /// by a positive <c>ThinkingBudget</c>, since thinking tokens bill as output too). Leave
        /// <c>MaxTokens</c> unset and only the input side is bounded — the real call's output cost is
        /// unknown until it completes. See the "why cumulative-first" design note in
        /// PLAN-cost-governance.md §4 for why an exact pre-flight bound isn't possible at all.
        /// </description></item>
        /// <item><description>
        /// <b>Enabling this doubles rate-limiter consumption per logical call</b> (the pre-flight
        /// <c>CountTokensAsync</c> call also goes through <c>GeminiClient.PostAsync</c>, so it consumes
        /// its own rate-limit permit) — factor that into <see cref="RateLimitOptions.RequestsPerMinute"/>
        /// if you enable it.
        /// </description></item>
        /// </list>
        /// The always-correct, primary mechanism remains <see cref="MaxCostPerDayUsd"/>, built from real
        /// billed usage after each call — use this alongside it, not instead of it.
        /// </remarks>
        public decimal? MaxCostPerRequestUsd { get; set; }

        /// <summary>
        /// Per-model USD pricing. Falls back to
        /// <see cref="Junaid.GoogleGemini.Net.Infrastructure.GeminiCostGovernor.DefaultPricing"/>
        /// (built-in, snapshot of Google's published rates at the time this library version shipped —
        /// WILL go stale; override here for accuracy) for any model not present in this dictionary. Keyed
        /// by the exact model string (e.g. <c>"gemini-3.6-flash"</c>).
        /// </summary>
        public IDictionary<string, ModelPricing>? ModelPricingOverrides { get; set; }
    }

    /// <summary>USD pricing per 1,000,000 tokens for one model.</summary>
    public class ModelPricing
    {
        /// <summary>USD per 1,000,000 standard-rate input tokens.</summary>
        public decimal InputPerMillionTokensUsd { get; set; }

        /// <summary>USD per 1,000,000 output tokens (includes "thinking"/thoughts tokens).</summary>
        public decimal OutputPerMillionTokensUsd { get; set; }

        /// <summary>Price for tokens served from context caching. Typically well below the standard input rate.</summary>
        public decimal CachedInputPerMillionTokensUsd { get; set; }

        /// <summary>
        /// Optional second pricing tier for models that charge more above a token threshold (currently
        /// gemini-2.5-pro and gemini-3.1-pro-preview both do, at 200k tokens). Null = flat rate regardless
        /// of prompt size (true for every other current model).
        /// </summary>
        public HighVolumeTier? HighVolumeTier { get; set; }
    }

    /// <summary>The above-threshold pricing tier for models that have one.</summary>
    public class HighVolumeTier
    {
        /// <summary>Token count above which the higher rate applies (compared against the request's
        /// actual PromptTokenCount, since the tier is determined by realized usage, not an estimate).</summary>
        public int ThresholdTokens { get; set; }

        /// <summary>USD per 1,000,000 standard-rate input tokens, above the threshold.</summary>
        public decimal InputPerMillionTokensUsd { get; set; }

        /// <summary>USD per 1,000,000 output tokens, above the threshold.</summary>
        public decimal OutputPerMillionTokensUsd { get; set; }

        /// <summary>USD per 1,000,000 cached-content input tokens, above the threshold.</summary>
        public decimal CachedInputPerMillionTokensUsd { get; set; }
    }

    /// <summary>
    /// Proxy configuration options
    /// </summary>
    public class ProxyOptions
    {
        /// <summary>
        /// Proxy server address
        /// </summary>
        [Required]
        public Uri Address { get; set; } = null!;

        /// <summary>
        /// Username for proxy authentication
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Password for proxy authentication
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Whether to bypass proxy for local addresses
        /// </summary>
        public bool BypassOnLocal { get; set; } = true;
    }
}