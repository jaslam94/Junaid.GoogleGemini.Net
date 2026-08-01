using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Junaid.GoogleGemini.Net.Testing.Tests;

/// <summary>
/// Wires up the real AddGemini + AddCassette DI pipeline against a <see cref="FakeInnerHandler"/>
/// standing in for the network, so tests exercise the actual handler chain (cassette → auth →
/// resilience → "network") rather than the cassette handler in isolation.
/// </summary>
internal static class CassetteTestHarness
{
    // Must pass GeminiOptionsValidator's format check (AIza/BIza/CIza prefix, 20+ chars) — this is
    // a fake key that should never appear in a cassette file, not a real credential.
    public const string ApiKey = "AIzaFAKE-TEST-KEY-MUST-NEVER-BE-RECORDED";

    public static IGeminiClient BuildClient(string cassettePath, CassetteMode mode, FakeInnerHandler inner)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddGemini(
            options => options.ApiKey = ApiKey,
            pipeline => pipeline.AddCassette(cassettePath, mode));

        // Registered after AddGemini, so this overrides the primary (innermost) handler only —
        // the cassette/auth/resilience handlers added above are untouched.
        services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => inner);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IGeminiClient>();
    }
}
