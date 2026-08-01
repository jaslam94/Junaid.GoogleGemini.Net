using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Junaid.GoogleGemini.Net.Testing;

/// <summary>DI entry points for cassette-based testing of Gemini calls.</summary>
public static class GeminiTestingExtensions
{
    /// <summary>
    /// Adds cassette recording/replay to the Gemini HTTP pipeline as the OUTERMOST handler — it
    /// runs before authentication, so a cassette file can never contain the API key and replay
    /// never needs one.
    /// </summary>
    /// <param name="builder">
    /// The pipeline builder passed into <c>AddGemini</c>'s <c>configurePipeline</c> parameter.
    /// </param>
    /// <param name="cassettePath">Path to the cassette JSON file (created on first recording).</param>
    /// <param name="mode">
    /// Defaults to <see cref="CassetteMode.RecordOnce"/>: replay what's already recorded, record
    /// anything new.
    /// </param>
    public static IHttpClientBuilder AddCassette(
        this IHttpClientBuilder builder, string cassettePath, CassetteMode mode = CassetteMode.RecordOnce)
    {
        if (string.IsNullOrWhiteSpace(cassettePath))
        {
            throw new ArgumentException("Cassette path must not be empty.", nameof(cassettePath));
        }

        // A cassette's in-memory replay cursor must survive the whole test run; the default
        // 2-minute handler rotation would otherwise silently reset it mid-suite.
        builder.SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        builder.AddHttpMessageHandler(() => new CassetteHandler(cassettePath, mode));
        return builder;
    }

    /// <summary>
    /// Convenience wrapper around <c>AddGemini</c> for the common case of a single cassette.
    /// Equivalent to <c>services.AddGemini(configureOptions, pipeline =&gt; pipeline.AddCassette(cassettePath, mode))</c>.
    /// </summary>
    public static IServiceCollection AddGeminiWithCassette(
        this IServiceCollection services,
        Action<GeminiOptions> configureOptions,
        string cassettePath,
        CassetteMode mode = CassetteMode.RecordOnce)
        => services.AddGemini(configureOptions, pipeline => pipeline.AddCassette(cassettePath, mode));
}
