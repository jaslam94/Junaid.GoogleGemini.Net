using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Implementation of the safety service for managing content safety
    /// </summary>
    public class SafetyService : ISafetyService
    {
        private static readonly string[] _allCategories = GeminiConstants.SafetyCategories.All;

        /// <inheritdoc/>
        public List<SafetySetting> CreateSafetySettings(string threshold)
        {
            return _allCategories.Select(category => new SafetySetting
            {
                Category = category,
                Threshold = threshold
            }).ToList();
        }

        /// <inheritdoc/>
        public List<SafetySetting> CreateSafetySettings(Dictionary<string, string> settings)
        {
            return settings.Select(kvp => new SafetySetting
            {
                Category = kvp.Key,
                Threshold = kvp.Value
            }).ToList();
        }

        /// <inheritdoc/>
        public Dictionary<string, string> AnalyzeSafetyRatings(GenerateContentResponse response)
        {
            var ratings = new Dictionary<string, string>();

            if (response.Candidates != null && response.Candidates.Length > 0)
            {
                var candidate = response.Candidates[0];
                if (candidate.SafetyRatings != null)
                {
                    foreach (var rating in candidate.SafetyRatings)
                    {
                        if (rating.Category is not null)
                        {
                            ratings[rating.Category] = rating.Probability ?? string.Empty;
                        }
                    }
                }
            }

            return ratings;
        }

        /// <inheritdoc/>
        public bool IsContentSafe(GenerateContentResponse response, Dictionary<string, string> thresholds)
        {
            var ratings = AnalyzeSafetyRatings(response);

            foreach (var threshold in thresholds)
            {
                if (ratings.TryGetValue(threshold.Key, out var probability))
                {
                    // Check if the probability exceeds the threshold
                    if (CompareSafetyLevels(probability, threshold.Value) > 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public List<SafetySetting> CreateStrictSafetySettings()
        {
            return CreateSafetySettings(new Dictionary<string, string>
            {
                { GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyThresholds.Low },
                { GeminiConstants.SafetyCategories.HateSpeech, GeminiConstants.SafetyThresholds.Low },
                { GeminiConstants.SafetyCategories.SexuallyExplicit, GeminiConstants.SafetyThresholds.Low },
                { GeminiConstants.SafetyCategories.DangerousContent, GeminiConstants.SafetyThresholds.Low }
            });
        }

        /// <inheritdoc/>
        public List<SafetySetting> CreateModerateSafetySettings()
        {
            return CreateSafetySettings(new Dictionary<string, string>
            {
                { GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyThresholds.Medium },
                { GeminiConstants.SafetyCategories.HateSpeech, GeminiConstants.SafetyThresholds.Medium },
                { GeminiConstants.SafetyCategories.SexuallyExplicit, GeminiConstants.SafetyThresholds.High },
                { GeminiConstants.SafetyCategories.DangerousContent, GeminiConstants.SafetyThresholds.Medium }
            });
        }

        /// <inheritdoc/>
        public List<SafetySetting> CreatePermissiveSafetySettings()
        {
            return CreateSafetySettings(new Dictionary<string, string>
            {
                { GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyThresholds.High },
                { GeminiConstants.SafetyCategories.HateSpeech, GeminiConstants.SafetyThresholds.High },
                { GeminiConstants.SafetyCategories.SexuallyExplicit, GeminiConstants.SafetyThresholds.High },
                { GeminiConstants.SafetyCategories.DangerousContent, GeminiConstants.SafetyThresholds.High }
            });
        }

        private int CompareSafetyLevels(string probability, string threshold)
        {
            var probabilityLevel = GetSafetyLevel(probability);
            var thresholdLevel = GetSafetyLevel(threshold);

            return probabilityLevel.CompareTo(thresholdLevel);
        }

        private int GetSafetyLevel(string level)
        {
            return level switch
            {
                // Probability vocabulary: what AnalyzeSafetyRatings/the API's SafetyRatings actually
                // return (GeminiConstants.SafetyProbabilities.*).
                GeminiConstants.SafetyProbabilities.Negligible => 0,
                GeminiConstants.SafetyProbabilities.Low => 1,
                GeminiConstants.SafetyProbabilities.Medium => 2,
                GeminiConstants.SafetyProbabilities.High => 3,

                // Threshold vocabulary (GeminiConstants.SafetyThresholds.*, e.g. "BLOCK_MEDIUM_AND_ABOVE"):
                // accepted here too, and mapped onto the same 0-3 scale as the matching-severity
                // probability. Bug fix: every other method on this class (CreateSafetySettings,
                // CreateStrictSafetySettings, ...) takes/produces SafetyThresholds.* strings, so a
                // caller reusing that same vocabulary for IsContentSafe's "thresholds" dictionary is the
                // natural, expected usage. But before this vocabulary was recognized here at all, any
                // BLOCK_* string fell through to -1, which (since every real probability level is >=0)
                // made CompareSafetyLevels return "unsafe" for essentially any response that had a
                // safety rating at all, regardless of how benign it actually was.
                GeminiConstants.SafetyThresholds.Low => 1,
                GeminiConstants.SafetyThresholds.Medium => 2,
                GeminiConstants.SafetyThresholds.High => 3,
                GeminiConstants.SafetyThresholds.None => int.MaxValue, // "block nothing" never trips as unsafe
                _ => -1
            };
        }
    }
}