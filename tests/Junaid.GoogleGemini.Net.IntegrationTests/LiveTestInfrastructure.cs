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
/// Like <see cref="RequiresGeminiKeyAttribute"/>, but also requires <c>GeminiPaidTier=1</c>, for
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
/// Like <see cref="RequiresGeminiKeyAttribute"/>, but also needs a small local video. Set
/// <c>GeminiTestVideo</c> to the path of an MP4 of about 10 seconds. No video lives in the repo, so the
/// test shows up as skipped, not passed, on any machine without one. A public YouTube URL is not an
/// option: Google returned HTTP 403 for one on 2026-10-10.
/// </summary>
public sealed class RequiresTestVideoAttribute : FactAttribute
{
    public RequiresTestVideoAttribute()
    {
        var path = Environment.GetEnvironmentVariable("GeminiTestVideo");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GeminiApiKey")))
        {
            Skip = "Set the GeminiApiKey environment variable to run live integration tests.";
        }
        else if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Skip = "Set GeminiTestVideo to the path of a small MP4 (about 10 seconds) to run this test.";
        }
    }
}

/// <summary>
/// Like <see cref="RequiresGeminiKeyAttribute"/>, but also needs a LONG video, to test the token savings that
/// Google claims for agentic video mode. Set <c>GeminiTestLongVideo</c> to the path of an MP4, MOV or WEBM of at
/// least three minutes. Google says the saving shows on long videos. On clips of about ten seconds, agentic used
/// slightly MORE tokens. A five-minute video costs roughly ten cents to test. The test skips without the variable.
/// </summary>
public sealed class RequiresLongTestVideoAttribute : FactAttribute
{
    public RequiresLongTestVideoAttribute()
    {
        var path = Environment.GetEnvironmentVariable("GeminiTestLongVideo");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GeminiApiKey")))
        {
            Skip = "Set the GeminiApiKey environment variable to run live integration tests.";
        }
        else if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Skip = "Set GeminiTestLongVideo to the path of a video of at least three minutes to run this test.";
        }
    }
}

/// <summary>
/// Needs two recordings of YOUR OWN voice, because Google's voice replication needs a real speaker and that speaker's
/// consent. Set <c>GeminiTestVoiceSource</c> to a 10 to 30 second clip of you speaking, and
/// <c>GeminiTestVoiceConsent</c> to a clip of you reading Google's consent statement (24 kHz mono 16-bit WAV is
/// recommended). Do not use anyone else's voice. Nothing in this repo creates or fakes a consent recording on purpose.
/// The test skips without both variables.
/// </summary>
public sealed class RequiresOwnVoiceRecordingsAttribute : FactAttribute
{
    public RequiresOwnVoiceRecordingsAttribute()
    {
        var source = Environment.GetEnvironmentVariable("GeminiTestVoiceSource");
        var consent = Environment.GetEnvironmentVariable("GeminiTestVoiceConsent");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GeminiApiKey")))
        {
            Skip = "Set the GeminiApiKey environment variable to run live integration tests.";
        }
        else if (Environment.GetEnvironmentVariable("GeminiPaidTier") != "1")
        {
            Skip = "Creating a voice runs a model. Set GeminiPaidTier=1 to run.";
        }
        else if (string.IsNullOrWhiteSpace(source) || !File.Exists(source) || string.IsNullOrWhiteSpace(consent) || !File.Exists(consent))
        {
            Skip = "Set GeminiTestVoiceSource and GeminiTestVoiceConsent to recordings of your own voice to run this test.";
        }
    }
}

/// <summary>
/// Builds a real DI container once for the whole suite (reading the key from the environment), the
/// same way a consuming app would. Constructing this does not call the API or validate the key.
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
            // gemini-2.5-flash was the original cheap default here but is no longer available to
            // new-user keys (confirmed live 2026-08-10: "This model ... is no longer available to new
            // users"). gemini-3.5-flash-lite is the library's current cheapest/fastest model
            // (GeminiConstants.Models.Fastest) and keeps the same "low cost/latency" intent.
            options.DefaultModel = "gemini-3.5-flash-lite";
        });
        services.AddGeminiChatClient("gemini-3.5-flash-lite");
        services.AddGeminiEmbeddingGenerator("gemini-embedding-001");

        Services = services.BuildServiceProvider();
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();
}

/// <summary>Groups all live tests into one collection so they share the fixture and run sequentially
/// (gentle on rate limits, deterministic ordering).</summary>
[CollectionDefinition("Live")]
public sealed class LiveCollection : ICollectionFixture<GeminiFixture>;
