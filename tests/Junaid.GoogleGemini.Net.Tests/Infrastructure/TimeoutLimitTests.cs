using System.ComponentModel.DataAnnotations;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// The request timeout used to stop at 300 seconds, in two places: a <c>[Range]</c> attribute and the
/// options validator. Google tells Flex users to allow 10 minutes or more per call, and tells Gemini 4
/// users to allow at least 15 minutes for hard prompts. These tests check every place that enforces
/// the limit, because changing only one of them leaves the other one to reject the value.
/// </summary>
public class TimeoutLimitTests
{
    private const string DummyKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345";

    [Fact]
    public void TheLimit_IsOneHour()
    {
        Assert.Equal(3600, GeminiConstants.Defaults.MaxTimeoutSeconds);
        Assert.Equal(100, GeminiConstants.Defaults.TimeoutSeconds); // the default does not change
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(300)]   // the old limit must still pass
    [InlineData(301)]   // the first value the old limit rejected
    [InlineData(900)]   // 15 minutes, for Gemini 4
    [InlineData(3600)]
    public void Validator_AcceptsTimeoutsUpToTheLimit(int seconds)
    {
        var result = new GeminiOptionsValidator().Validate(null, new GeminiOptions { ApiKey = DummyKey, TimeoutSeconds = seconds });

        Assert.True(result.Succeeded, string.Join("; ", result.Failures ?? []));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(3601)]
    public void Validator_RejectsTimeoutsOutsideTheLimit(int seconds)
    {
        var result = new GeminiOptionsValidator().Validate(null, new GeminiOptions { ApiKey = DummyKey, TimeoutSeconds = seconds });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("between 1 and 3600"));
    }

    [Theory]
    [InlineData(900, true)]
    [InlineData(3600, true)]
    [InlineData(3601, false)]
    [InlineData(0, false)]
    public void RangeAttribute_MatchesTheValidator(int seconds, bool valid)
    {
        var options = new GeminiOptions { ApiKey = DummyKey, TimeoutSeconds = seconds };
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.Equal(valid, ok);
        if (!valid)
        {
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(GeminiOptions.TimeoutSeconds)));
        }
    }

    [Fact]
    public void AddGemini_AcceptsALongTimeout_AndAppliesItToTheHttpClient()
    {
        // This goes through the real DI registration. That path runs both checks, so it fails if either
        // one still rejects the value. It also proves that the value reaches HttpClient.Timeout.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = DummyKey;
            o.TimeoutSeconds = 1800;
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value; // throws if invalid
        using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GeminiClient));

        Assert.Equal(1800, options.TimeoutSeconds);
        Assert.Equal(TimeSpan.FromMinutes(30), http.Timeout);
    }

    [Fact]
    public void AddGemini_RejectsATimeoutAboveTheLimit()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = DummyKey;
            o.TimeoutSeconds = 3601;
        });
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GeminiOptions>>().Value);
    }
}
