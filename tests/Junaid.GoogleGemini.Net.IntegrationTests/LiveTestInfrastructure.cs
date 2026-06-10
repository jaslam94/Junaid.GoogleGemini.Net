using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips the test unless the <c>GeminiApiKey</c> environment
/// variable is set. Keeps the live suite opt-in so it never breaks ordinary CI runs.
/// </summary>
public sealed class RequiresGeminiKeyAttribute : FactAttribute
{
    public RequiresGeminiKeyAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GeminiApiKey")))
        {
            Skip = "Set the GeminiApiKey environment variable to run live integration tests.";
        }
    }
}

/// <summary>
/// Like <see cref="RequiresGeminiKeyAttribute"/>, but also requires <c>GeminiPaidTier=1</c> — for
/// features the Gemini free tier blocks outright (e.g. context-cache storage has a free-tier limit
/// of 0). Skips cleanly otherwise so the suite stays green on free-tier keys.
/// </summary>
public sealed class RequiresPaidGeminiKeyAttribute : FactAttribute
{
    public RequiresPaidGeminiKeyAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GeminiApiKey")))
        {
            Skip = "Set the GeminiApiKey environment variable to run live integration tests.";
        }
        else if (Environment.GetEnvironmentVariable("GeminiPaidTier") != "1")
        {
            Skip = "Requires a billing-enabled key (free tier blocks this feature). Set GeminiPaidTier=1 to run.";
        }
    }
}

/// <summary>
/// Builds a real DI container once for the whole suite (reading the key from the environment), the
/// same way a consuming app would. Constructing this does not call the API or validate the key —
/// validation happens lazily on first use, which only occurs when a test actually runs.
/// </summary>
public sealed class GeminiFixture
{
    public IServiceProvider Services { get; }

    public GeminiFixture()
    {
        var key = Environment.GetEnvironmentVariable("GeminiApiKey");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(options =>
        {
            // A valid-format placeholder so the container builds even when tests will be skipped.
            options.ApiKey = string.IsNullOrWhiteSpace(key) ? "AIzaSyPLACEHOLDER0123456789ABCDEFGHIJK" : key;
            options.DefaultModel = "gemini-2.5-flash";
        });
        services.AddGeminiChatClient("gemini-2.5-flash");
        services.AddGeminiEmbeddingGenerator("gemini-embedding-001");

        Services = services.BuildServiceProvider();
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();
}

/// <summary>Groups all live tests into one collection so they share the fixture and run sequentially
/// (gentle on rate limits, deterministic ordering).</summary>
[CollectionDefinition("Live")]
public sealed class LiveCollection : ICollectionFixture<GeminiFixture>;
