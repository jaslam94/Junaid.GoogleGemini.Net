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

            if (response.candidates != null && response.candidates.Length > 0)
            {
                var candidate = response.candidates[0];
                if (candidate.safetyRatings != null)
                {
                    foreach (var rating in candidate.safetyRatings)
                    {
                        ratings[rating.category] = rating.probability;
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
                GeminiConstants.SafetyProbabilities.Negligible => 0,
                GeminiConstants.SafetyProbabilities.Low => 1,
                GeminiConstants.SafetyProbabilities.Medium => 2,
                GeminiConstants.SafetyProbabilities.High => 3,
                _ => -1
            };
        }
    }
}