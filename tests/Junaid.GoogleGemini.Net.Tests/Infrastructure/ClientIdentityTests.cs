using System.Diagnostics;
using System.Net;
using System.Reflection;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// How the client identifies itself. Google asks partners to send <c>x-goog-api-client</c> in the form
/// <c>company-product/version</c>. The telemetry source also carries a version. That version was a
/// hand-written constant that still said 6.4.1 after seven more releases, so these tests compare it
/// with the real assembly version.
/// </summary>
public class ClientIdentityTests
{
    private const string SuccessJson =
        """{"candidates":[{"content":{"role":"model","parts":[{"text":"ok"}]},"finishReason":"STOP"}]}""";

    // Read the version directly from the assembly, without using the library's own helper. A test that
    // used the helper could not catch a bug in the helper.
    private static string ExpectedVersion()
    {
        var info = typeof(GeminiClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var plus = info.IndexOf('+');
        return plus >= 0 ? info.Substring(0, plus) : info;
    }

    [Fact]
    public async Task EveryRequest_CarriesTheClientHeader_AndAVersionedUserAgent()
    {
        var fake = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, SuccessJson);
        var client = Build(fake);

        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/x:generateContent", new GenerateContentRequest());

        var request = Assert.Single(fake.Requests);
        var version = ExpectedVersion();
        Assert.Equal($"junaid-googlegemini-net/{version}", Assert.Single(request.Headers.GetValues("x-goog-api-client")));
        Assert.Contains($"Junaid.GoogleGemini.Net/{version}", request.Headers.UserAgent.ToString());
        Assert.True(request.Headers.Contains("x-goog-api-key")); // the key header still goes out
    }

    [Fact]
    public async Task ARetriedRequest_SendsTheClientHeaderOnce()
    {
        // The retry handler re-sends the same request message. The header must not pile up.
        var fake = FakeHttpMessageHandler.Sequence(
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.ServiceUnavailable, "{}"),
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SuccessJson));
        var client = Build(fake);

        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/x:generateContent", new GenerateContentRequest());

        Assert.Equal(2, fake.CallCount);
        foreach (var request in fake.Requests)
        {
            Assert.Single(request.Headers.GetValues("x-goog-api-client"));
        }
    }

    [Fact]
    public async Task AHeaderSetBeforeTheAuthHandler_IsKept()
    {
        var fake = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, SuccessJson);
        var client = Build(fake, configurePipeline: b => b.AddHttpMessageHandler(() => new SetClientHeaderHandler("my-app/9.9")));

        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/x:generateContent", new GenerateContentRequest());

        var request = Assert.Single(fake.Requests);
        Assert.Equal("my-app/9.9", Assert.Single(request.Headers.GetValues("x-goog-api-client")));
    }

    [Fact]
    public async Task TheTelemetrySource_ReportsTheRealPackageVersion()
    {
        // Make one call first so that the static telemetry class is initialized in this process.
        var client = Build(FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, SuccessJson));
        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/x:generateContent", new GenerateContentRequest());

        // ShouldListenTo is called once for every source that already exists when a listener is added.
        var seen = new List<string?>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
            {
                if (source.Name == "Junaid.GoogleGemini.Net")
                {
                    seen.Add(source.Version);
                }
                return false;
            },
        };
        ActivitySource.AddActivityListener(listener);

        Assert.Equal(ExpectedVersion(), Assert.Single(seen));
    }

    private static IGeminiClient Build(FakeHttpMessageHandler fake, Action<IHttpClientBuilder>? configurePipeline = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345";
            o.BaseUrl = new Uri("https://example.test/v1beta/");
            o.RetryBaseDelay = TimeSpan.Zero;
            o.RateLimit.Enabled = false;
        }, configurePipeline);

        services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => fake);
        return services.BuildServiceProvider().GetRequiredService<IGeminiClient>();
    }

    private sealed class SetClientHeaderHandler(string value) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("x-goog-api-client", value);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
