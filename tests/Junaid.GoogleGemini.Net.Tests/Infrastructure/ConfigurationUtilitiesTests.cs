using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// Regression coverage for <see cref="ConfigurationUtilities.IsValidApiKeyFormat"/>: it used to be
/// locked to the legacy "AIza"/"BIza"/"CIza" prefixes, which rejected the newer "AQ." key format
/// Google started issuing from AI Studio in 2026, a real key, format-rejected client-side before it
/// ever reached the network. There was no test coverage for this method at all before this bug shipped.
/// </summary>
public class ConfigurationUtilitiesTests
{
    [Theory]
    [InlineData("AIzaSyDUMMY_KEY_LOOKS_LIKE_A_LEGACY_KEY_1234")] // legacy format, must still work
    [InlineData("AQ.Ab1234567890DUMMY_NEW_FORMAT_KEY_EXAMPLE")]  // new format (2026 rollout)
    [InlineData("some-other-future-prefix-nobody-has-invented-yet")] // must not be prefix-locked
    public void IsValidApiKeyFormat_AcceptsPlausibleKeysRegardlessOfPrefix(string apiKey)
    {
        Assert.True(ConfigurationUtilities.IsValidApiKeyFormat(apiKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]                    // too short to plausibly be a real key
    [InlineData("AIza key with spaces here")] // whitespace strongly suggests a paste error
    public void IsValidApiKeyFormat_RejectsObviousMistakes(string? apiKey)
    {
        Assert.False(ConfigurationUtilities.IsValidApiKeyFormat(apiKey!));
    }
}
