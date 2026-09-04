using System.Collections.Concurrent;
using System.Globalization;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.Infrastructure;

/// <summary>Tracks and enforces a cumulative daily USD budget for Gemini generation calls.</summary>
public interface ICostGovernor
{
    /// <summary>
    /// Checked BEFORE a call is sent. Throws <see cref="GeminiBudgetExceededException"/> if today's
    /// (UTC) cumulative spend has already reached the configured daily ceiling. Cheap: in-memory only,
    /// no network call.
    /// </summary>
    void CheckBudget();

    /// <summary>
    /// Called AFTER a successful response, with its real usage. Computes actual USD cost, adds it to
    /// today's running total, and records the <c>gemini.client.cost.usd</c> OTel metric (see the
    /// "Telemetry" section of PLAN-cost-governance.md §6.2). Returns the cost of this one call, in
    /// case the caller wants to log/expose it. Returns <c>0</c> (without recording anything) when the
    /// model has no known pricing. See <see cref="GeminiCostGovernor.DefaultPricing"/>.
    /// </summary>
    decimal RecordSpend(string? model, UsageMetadata usage);

    /// <summary>Today's (UTC) cumulative recorded spend, for diagnostics/exposure.</summary>
    decimal GetTodaySpend();

    /// <summary>
    /// Cheap, in-memory check for whether <see cref="BudgetOptions.MaxCostPerRequestUsd"/> is
    /// configured and enforced. Callers use this to decide whether it's worth spending an extra
    /// <c>CountTokensAsync</c> round-trip on <see cref="CheckEstimatedRequestCost"/> at all. When this
    /// is <c>false</c>, skip straight to sending the request.
    /// </summary>
    bool HasRequestCeiling { get; }

    /// <summary>
    /// Best-effort pre-flight check for <see cref="BudgetOptions.MaxCostPerRequestUsd"/>, called BEFORE
    /// a request is sent. <paramref name="inputTokens"/> should be an exact count (from
    /// <c>CountTokensAsync</c>); <paramref name="maxOutputTokens"/> should be the request's
    /// <c>MaxTokens</c> (plus any positive <c>ThinkingBudget</c>, since thinking tokens bill as output
    /// too), or <c>null</c> if the caller didn't set one. In that case only the input side of the
    /// estimate is bounded. No-op (never throws, returns <c>0</c>) when
    /// <see cref="BudgetOptions.MaxCostPerRequestUsd"/> is unset, when enforcement is disabled, or when
    /// the model has no pricing entry. Throws <see cref="GeminiRequestCostExceededException"/> if the
    /// estimate exceeds the ceiling.
    /// </summary>
    /// <returns>The estimated cost in USD (0 if not estimated).</returns>
    decimal CheckEstimatedRequestCost(string? model, int inputTokens, int? maxOutputTokens);
}

/// <summary>Default <see cref="ICostGovernor"/> implementation: in-memory, single-process, UTC-daily.</summary>
/// <remarks>
/// <b>In-process only.</b> This budget is tracked in-memory, per process. If your app runs as multiple
/// instances (e.g. horizontally scaled), each instance tracks its own spend independently. The
/// effective ceiling is <c>MaxCostPerDayUsd × instance count</c>, not a shared global cap. For a true
/// cross-instance cap, feed the <c>gemini.client.cost.usd</c> metric into an external system (a shared
/// counter, your APM) and enforce there. This library does not attempt that.
/// </remarks>
public sealed class GeminiCostGovernor : ICostGovernor
{
    /// <summary>
    /// Built-in USD-per-1,000,000-token pricing, re-verified against
    /// <see href="https://ai.google.dev/gemini-api/docs/pricing">Google's published pricing page</see>
    /// on 2026-08-15 (paid tier). This snapshot WILL go stale as Google updates prices. Override via
    /// <see cref="BudgetOptions.ModelPricingOverrides"/> for accuracy, especially in production.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ModelPricing> DefaultPricing =
        new Dictionary<string, ModelPricing>(StringComparer.Ordinal)
        {
            // gemini-3.6-flash, gemini-3.7-flash, and gemini-3.8-flash all share the same introductory
            // rate, in effect since 3.7's August 13, 2026 launch (Google moved 3.6-flash onto it at the
            // same time; 3.8-flash launched September 2, 2026 already at this rate), through December
            // 31, 2026. Standard rate (double these numbers: $1.50/$7.50/$0.15) resumes January 1, 2027.
            // Re-verify and update this table again before then.
            ["gemini-3.8-flash"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.75m,
                OutputPerMillionTokensUsd = 3.75m,
                CachedInputPerMillionTokensUsd = 0.075m,
            },
            ["gemini-3.7-flash"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.75m,
                OutputPerMillionTokensUsd = 3.75m,
                CachedInputPerMillionTokensUsd = 0.075m,
            },
            ["gemini-3.6-flash"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.75m,
                OutputPerMillionTokensUsd = 3.75m,
                CachedInputPerMillionTokensUsd = 0.075m,
            },
            ["gemini-3.5-flash"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 1.50m,
                OutputPerMillionTokensUsd = 9.00m,
                CachedInputPerMillionTokensUsd = 0.15m,
            },
            ["gemini-3.5-flash-lite"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.30m,
                OutputPerMillionTokensUsd = 2.50m,
                CachedInputPerMillionTokensUsd = 0.03m,
            },
            ["gemini-3.1-pro-preview"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 2.00m,
                OutputPerMillionTokensUsd = 12.00m,
                CachedInputPerMillionTokensUsd = 0.20m,
                HighVolumeTier = new HighVolumeTier
                {
                    ThresholdTokens = 200_000,
                    InputPerMillionTokensUsd = 4.00m,
                    OutputPerMillionTokensUsd = 18.00m,
                    CachedInputPerMillionTokensUsd = 0.40m,
                },
            },
            ["gemini-3-flash-preview"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.50m,
                OutputPerMillionTokensUsd = 3.00m,
                CachedInputPerMillionTokensUsd = 0.05m,
            },
            ["gemini-3.1-flash-lite"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.25m,
                OutputPerMillionTokensUsd = 1.50m,
                CachedInputPerMillionTokensUsd = 0.025m,
            },
            ["gemini-2.5-pro"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 1.25m,
                OutputPerMillionTokensUsd = 10.00m,
                CachedInputPerMillionTokensUsd = 0.125m,
                HighVolumeTier = new HighVolumeTier
                {
                    ThresholdTokens = 200_000,
                    InputPerMillionTokensUsd = 2.50m,
                    OutputPerMillionTokensUsd = 15.00m,
                    CachedInputPerMillionTokensUsd = 0.25m,
                },
            },
            ["gemini-2.5-flash"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.30m,
                OutputPerMillionTokensUsd = 2.50m,
                CachedInputPerMillionTokensUsd = 0.03m,
            },

            // Native image generation models. Confirmed (2026-08-15) these ARE priced per-1M-tokens on
            // both sides, same shape as text models. The "likely per-image, needs separate design"
            // assumption in PLAN-cost-governance.md §11 turned out to be wrong for these three; only
            // gemini-2.5-flash-image (older, omitted below) is priced strictly per-image with no
            // token-rate equivalent published, so it still can't be represented here without guessing a
            // token-per-image conversion.
            //
            // Two nuances specific to this group, worth reading before touching these numbers:
            // (1) OutputPerMillionTokensUsd below is the IMAGE output rate. gemini-3-pro-image and
            //     gemini-3.1-flash-lite-image actually publish a cheaper second output rate for any
            //     text/thinking tokens in the same response ("$12.00 (text and thinking) $120.00
            //     (images)" for pro, similarly for flash-lite), but UsageMetadata has no field that
            //     breaks candidatesTokenCount down by modality, so there's no way to price the two
            //     portions separately today. Using the image (higher) rate for 100% of output tokens
            //     overcounts any text/thinking portion, which is the safe direction for a budget
            //     ceiling (fails closed, not open). Just don't mistake this for an exact figure.
            // (2) Only gemini-3.1-flash-image has a published cached-input rate ($0.25/1M, confirmed
            //     directly on the pricing page). That is HALF the standard input rate, not "no discount".
            //     gemini-3-pro-image and gemini-3.1-flash-lite-image publish no cached rate at all for
            //     the standard tier, so those two fall back to "= standard input rate" (assume no
            //     discount) rather than 0, since 0 would silently price cached input as free.
            ["gemini-3.1-flash-image"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.50m,
                OutputPerMillionTokensUsd = 60.00m,
                CachedInputPerMillionTokensUsd = 0.25m,
            },
            ["gemini-3-pro-image"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 2.00m,
                OutputPerMillionTokensUsd = 120.00m,
                CachedInputPerMillionTokensUsd = 2.00m,
            },
            ["gemini-3.1-flash-lite-image"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.25m,
                OutputPerMillionTokensUsd = 30.00m,
                CachedInputPerMillionTokensUsd = 0.25m,
            },

            // Text-to-speech models. Confirmed live (2026-09-04, see PLAN-tts.md) that audio output
            // tokens arrive as a plain CandidatesTokenCount, tagged AUDIO in CandidatesTokensDetails,
            // the same UsageMetadata shape every other model uses. That means 100% of output here is
            // priced at the audio rate below, which is correct for TTS (unlike the image models
            // above, a TTS response has no separate text output to under-or-over-count). None of the
            // three publish a cached-input rate, so all three fall back to the standard input rate
            // (assume no discount) rather than 0, matching the same-situation image models above.
            // Pricing numbers themselves came from a single fetch of the pricing page, not a live
            // billing check; re-verify before relying on them, same as every entry in this table.
            ["gemini-2.5-flash-preview-tts"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 0.50m,
                OutputPerMillionTokensUsd = 10.00m,
                CachedInputPerMillionTokensUsd = 0.50m,
            },
            ["gemini-2.5-pro-preview-tts"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 1.00m,
                OutputPerMillionTokensUsd = 20.00m,
                CachedInputPerMillionTokensUsd = 1.00m,
            },
            ["gemini-3.1-flash-tts-preview"] = new ModelPricing
            {
                InputPerMillionTokensUsd = 1.00m,
                OutputPerMillionTokensUsd = 20.00m,
                CachedInputPerMillionTokensUsd = 1.00m,
            },
        };

    // Keyed by the UTC calendar day, normalized to midnight (DateTime.Date), NOT System.DateOnly:
    // DateOnly isn't available on netstandard2.0 (this library's lowest target) without a runtime
    // polyfill this project doesn't carry, whereas DateTime.Date works everywhere and gives the same
    // "no time-of-day/timezone ambiguity in the key" property as long as it's always derived from
    // DateTime.UtcNow (never DateTime.Now).
    private readonly ConcurrentDictionary<DateTime, decimal> _dailySpend = new();
    private readonly ConcurrentDictionary<string, bool> _warnedUnpricedModels = new(StringComparer.Ordinal);
    private readonly BudgetOptions? _options;
    private readonly Action<string?, decimal> _recordCost;
    private readonly ILogger<GeminiCostGovernor>? _logger;
    private readonly Func<DateTime> _utcNowProvider;

    /// <summary>Initializes a new instance of the <see cref="GeminiCostGovernor"/>.</summary>
    /// <param name="options">
    /// Budget configuration, or <c>null</c> to disable the feature entirely (zero overhead: neither
    /// enforcement nor cost tracking/recording runs). This is what a <c>null</c>
    /// <see cref="GeminiOptions.Budget"/> maps to.
    /// </param>
    /// <param name="recordCost">Callback invoked with (model, costUsd) for each priced call, e.g. <c>GeminiTelemetry.RecordCost</c>.</param>
    /// <param name="logger">Optional logger, used for the one-time-per-model "unpriced model" warning.</param>
    /// <param name="utcNowProvider">Optional clock override, for testing day-rollover behavior. Defaults to <see cref="DateTime.UtcNow"/>.</param>
    public GeminiCostGovernor(
        BudgetOptions? options,
        Action<string?, decimal> recordCost,
        ILogger<GeminiCostGovernor>? logger = null,
        Func<DateTime>? utcNowProvider = null)
    {
        _options = options;
        _recordCost = recordCost ?? throw new ArgumentNullException(nameof(recordCost));
        _logger = logger;
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a cost governor with enforcement disabled but cost tracking/recording still active.
    /// This mirrors <see cref="GeminiRateLimiter.CreateDisabled"/>'s "always succeeds" semantics rather than
    /// "does nothing at all". Useful for tests, or to keep observability on while opting out of the
    /// daily-budget rejection behavior.
    /// </summary>
    public static GeminiCostGovernor CreateDisabled() =>
        new(new BudgetOptions { Enabled = false }, GeminiTelemetry.RecordCost);

    /// <inheritdoc/>
    public void CheckBudget()
    {
        if (_options is not { Enabled: true, MaxCostPerDayUsd: { } limit })
        {
            return;
        }

        var spend = GetTodaySpend();
        if (spend >= limit)
        {
            throw new GeminiBudgetExceededException(
                $"Daily Gemini cost budget of ${limit.ToString("0.00", CultureInfo.InvariantCulture)} USD " +
                $"has been reached (${spend.ToString("0.00", CultureInfo.InvariantCulture)} spent so far " +
                "today, UTC). The call was rejected before being sent, so it cost nothing.",
                spend,
                limit);
        }
    }

    /// <inheritdoc/>
    public decimal RecordSpend(string? model, UsageMetadata usage)
    {
        if (usage is null)
        {
            throw new ArgumentNullException(nameof(usage));
        }

        // Budget == null means the feature isn't configured at all: zero overhead, not even the cost
        // computation below runs. This is distinct from Enabled == false (BudgetOptions still present,
        // enforcement inert, but tracking/recording stays on. See CreateDisabled()).
        if (_options is null)
        {
            return 0m;
        }

        var cost = ComputeCost(model, usage);
        if (cost is null)
        {
            WarnUnpricedModelOnce(model);
            return 0m;
        }

        var today = _utcNowProvider().Date;
        _dailySpend.AddOrUpdate(today, cost.Value, (_, existing) => existing + cost.Value);
        PruneOldEntries(today);

        _recordCost(model, cost.Value);
        return cost.Value;
    }

    /// <inheritdoc/>
    public decimal GetTodaySpend()
    {
        if (_options is null)
        {
            return 0m;
        }

        var today = _utcNowProvider().Date;
        return _dailySpend.TryGetValue(today, out var spend) ? spend : 0m;
    }

    /// <inheritdoc/>
    public bool HasRequestCeiling => _options is { Enabled: true, MaxCostPerRequestUsd: not null };

    /// <inheritdoc/>
    public decimal CheckEstimatedRequestCost(string? model, int inputTokens, int? maxOutputTokens)
    {
        if (_options is not { Enabled: true, MaxCostPerRequestUsd: { } limit })
        {
            return 0m;
        }

        var pricing = ResolvePricing(model);
        if (pricing is null)
        {
            // Nothing to estimate against. Same "skip, don't guess $0" stance as RecordSpend for an
            // unpriced model. The call is allowed through rather than silently blocked.
            WarnUnpricedModelOnce(model);
            return 0m;
        }

        var rates = SelectRates(pricing, inputTokens);

        // Pre-flight estimate limitations vs. the exact post-call cost in ComputeCost:
        //  - No context-caching discount: we don't know ahead of time how much of the input Gemini
        //    will actually serve from cache, so the FULL input token count is priced at the standard
        //    (non-cached) rate. This makes the estimate conservative (>= actual) for calls that use
        //    CachedContent, never an under-estimate on that axis.
        //  - Output is only bounded when the caller set MaxTokens (optionally widened by a positive
        //    ThinkingBudget, since thinking tokens bill as output too (see ComputeCost). If MaxTokens
        //    is unset, maxOutputTokens is null and only the input side is bounded; the real call could
        //    cost more than this estimate once output tokens are billed.
        var inputCost = Math.Max(0, inputTokens) / 1_000_000m * rates.Input;
        var outputCost = maxOutputTokens is { } bound ? Math.Max(0, bound) / 1_000_000m * rates.Output : 0m;
        var estimate = inputCost + outputCost;

        if (estimate > limit)
        {
            var outputCaveat = maxOutputTokens is null
                ? " Note: no MaxTokens was set on the request, so this estimate only bounds input cost -- the real call's output cost is unbounded and could push the actual cost higher (or lower)."
                : "";
            throw new GeminiRequestCostExceededException(
                $"Estimated cost of this request (${estimate.ToString("0.00", CultureInfo.InvariantCulture)} USD) " +
                $"exceeds the configured per-request ceiling (${limit.ToString("0.00", CultureInfo.InvariantCulture)} USD)." +
                outputCaveat,
                estimate,
                limit);
        }

        return estimate;
    }

    // Cost formula (see PLAN-cost-governance.md §6.3):
    //   tier = usage.PromptTokenCount > pricing.HighVolumeTier?.ThresholdTokens ? HighVolumeTier : base
    //   billableInputTokens = PromptTokenCount - CachedContentTokenCount
    //   inputCost  = billableInputTokens / 1e6 * tier.InputPerMillionTokensUsd
    //   cachedCost = CachedContentTokenCount / 1e6 * tier.CachedInputPerMillionTokensUsd
    //   outputCost = (CandidatesTokenCount + ThoughtsTokenCount) / 1e6 * tier.OutputPerMillionTokensUsd
    // ThoughtsTokenCount is billed as OUTPUT per Gemini's own docs (see the XML doc comment on
    // UsageMetadata.ThoughtsTokenCount). Pricing only CandidatesTokenCount would silently undercount
    // every thinking-enabled call. All math is `decimal`; only the final OTel Counter<double>.Record
    // call (in GeminiTelemetry.RecordCost) casts to double.
    private decimal? ComputeCost(string? model, UsageMetadata usage)
    {
        var pricing = ResolvePricing(model);
        if (pricing is null)
        {
            return null;
        }

        var rates = SelectRates(pricing, usage.PromptTokenCount);

        var billableInputTokens = Math.Max(0, usage.PromptTokenCount - usage.CachedContentTokenCount);
        var inputCost = billableInputTokens / 1_000_000m * rates.Input;
        var cachedCost = usage.CachedContentTokenCount / 1_000_000m * rates.Cached;
        var outputCost = (usage.CandidatesTokenCount + usage.ThoughtsTokenCount) / 1_000_000m * rates.Output;

        return inputCost + cachedCost + outputCost;
    }

    // Shared by ComputeCost (exact, post-call) and CheckEstimatedRequestCost (pre-flight estimate):
    // both select the high-volume tier the same way: strictly above the threshold, based on the
    // request's actual/counted input token count, never an assumption.
    private static (decimal Input, decimal Output, decimal Cached) SelectRates(ModelPricing pricing, int inputTokensForTierSelection)
    {
        var tier = inputTokensForTierSelection > pricing.HighVolumeTier?.ThresholdTokens
            ? pricing.HighVolumeTier
            : null;

        return (
            tier?.InputPerMillionTokensUsd ?? pricing.InputPerMillionTokensUsd,
            tier?.OutputPerMillionTokensUsd ?? pricing.OutputPerMillionTokensUsd,
            tier?.CachedInputPerMillionTokensUsd ?? pricing.CachedInputPerMillionTokensUsd);
    }

    private ModelPricing? ResolvePricing(string? model)
    {
        if (model is null)
        {
            return null;
        }

        if (_options?.ModelPricingOverrides is { } overrides && overrides.TryGetValue(model, out var overridden))
        {
            return overridden;
        }

        return DefaultPricing.TryGetValue(model, out var builtin) ? builtin : null;
    }

    private void WarnUnpricedModelOnce(string? model)
    {
        var key = model ?? "(unknown)";
        if (_warnedUnpricedModels.TryAdd(key, true))
        {
            _logger?.LogWarning(
                "Gemini cost governance has no pricing entry for model {Model}; its cost will not be " +
                "tracked or counted toward the daily budget (token usage is still recorded as normal). " +
                "Add an entry to GeminiOptions.Budget.ModelPricingOverrides to fix this.",
                key);
        }
    }

    private void PruneOldEntries(DateTime today)
    {
        var cutoff = today.AddDays(-2);
        foreach (var day in _dailySpend.Keys)
        {
            if (day < cutoff)
            {
                _dailySpend.TryRemove(day, out _);
            }
        }
    }
}
