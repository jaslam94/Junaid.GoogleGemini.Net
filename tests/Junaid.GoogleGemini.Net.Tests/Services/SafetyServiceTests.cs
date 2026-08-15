using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

// Regression coverage for a real bug: IsContentSafe's "thresholds" parameter silently only accepted
// GeminiConstants.SafetyProbabilities.* strings ("NEGLIGIBLE"/"LOW"/"MEDIUM"/"HIGH"), while every
// other method on ISafetyService (CreateSafetySettings, CreateStrictSafetySettings, ...) takes/
// produces GeminiConstants.SafetyThresholds.* strings ("BLOCK_LOW_AND_ABOVE", etc.) — the vocabulary
// a caller would naturally reuse. Passing that natural vocabulary used to make every check report
// "unsafe" regardless of actual content, because the unrecognized threshold string mapped to -1,
// which any real (>=0) probability level compares greater than. This had zero prior test coverage.
public class SafetyServiceTests
{
    private static GenerateContentResponse ResponseWithRating(string category, string probability) =>
        new()
        {
            Candidates =
            [
                new Candidate
                {
                    SafetyRatings =
                    [
                        new SafetyRating { Category = category, Probability = probability }
                    ]
                }
            ]
        };

    [Fact]
    public void IsContentSafe_WithBlockThresholdVocabulary_BenignContentPasses()
    {
        var service = new SafetyService();
        var response = ResponseWithRating(
            GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyProbabilities.Negligible);

        // The natural pairing: reuse the same threshold vocabulary CreateSafetySettings uses.
        var thresholds = new Dictionary<string, string>
        {
            [GeminiConstants.SafetyCategories.Harassment] = GeminiConstants.SafetyThresholds.Medium
        };

        Assert.True(service.IsContentSafe(response, thresholds));
    }

    [Fact]
    public void IsContentSafe_WithBlockThresholdVocabulary_ExceedingContentFails()
    {
        var service = new SafetyService();
        var response = ResponseWithRating(
            GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyProbabilities.High);

        var thresholds = new Dictionary<string, string>
        {
            [GeminiConstants.SafetyCategories.Harassment] = GeminiConstants.SafetyThresholds.Medium
        };

        Assert.False(service.IsContentSafe(response, thresholds));
    }

    [Fact]
    public void IsContentSafe_BlockNoneThreshold_NeverFails()
    {
        var service = new SafetyService();
        var response = ResponseWithRating(
            GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyProbabilities.High);

        var thresholds = new Dictionary<string, string>
        {
            [GeminiConstants.SafetyCategories.Harassment] = GeminiConstants.SafetyThresholds.None
        };

        Assert.True(service.IsContentSafe(response, thresholds));
    }

    [Fact]
    public void IsContentSafe_WithProbabilityVocabulary_StillWorks()
    {
        // The pre-existing, already-working call pattern (used by the live test suite) must keep working.
        var service = new SafetyService();
        var response = ResponseWithRating(
            GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyProbabilities.Negligible);

        var thresholds = new Dictionary<string, string>
        {
            [GeminiConstants.SafetyCategories.Harassment] = GeminiConstants.SafetyProbabilities.Medium
        };

        Assert.True(service.IsContentSafe(response, thresholds));
    }
}
