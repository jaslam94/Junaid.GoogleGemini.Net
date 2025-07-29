using Junaid.GoogleGemini.Net.Infrastructure.Constants;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Implementation of the safety service for managing content safety
    /// </summary>
    public class SafetyService : ISafetyService
    {
        private readonly string[] _allCategories = new[]
        {
            SafetyCategory.Harassment,
            SafetyCategory.HateSpeech,
            SafetyCategory.SexuallyExplicit,
            SafetyCategory.DangerousContent,
            SafetyCategory.Deceptive
        };

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
                { SafetyCategory.Harassment, SafetyThreshold.Low },
                { SafetyCategory.HateSpeech, SafetyThreshold.Low },
                { SafetyCategory.SexuallyExplicit, SafetyThreshold.Low },
                { SafetyCategory.DangerousContent, SafetyThreshold.Low },
                { SafetyCategory.Deceptive, SafetyThreshold.Low }
            });
        }

        /// <inheritdoc/>
        public List<SafetySetting> CreateModerateSafetySettings()
        {
            return CreateSafetySettings(new Dictionary<string, string>
            {
                { SafetyCategory.Harassment, SafetyThreshold.Medium },
                { SafetyCategory.HateSpeech, SafetyThreshold.Medium },
                { SafetyCategory.SexuallyExplicit, SafetyThreshold.High },
                { SafetyCategory.DangerousContent, SafetyThreshold.Medium },
                { SafetyCategory.Deceptive, SafetyThreshold.Medium }
            });
        }

        /// <inheritdoc/>
        public List<SafetySetting> CreatePermissiveSafetySettings()
        {
            return CreateSafetySettings(new Dictionary<string, string>
            {
                { SafetyCategory.Harassment, SafetyThreshold.High },
                { SafetyCategory.HateSpeech, SafetyThreshold.High },
                { SafetyCategory.SexuallyExplicit, SafetyThreshold.High },
                { SafetyCategory.DangerousContent, SafetyThreshold.High },
                { SafetyCategory.Deceptive, SafetyThreshold.High }
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
                SafetyProbability.Negligible => 0,
                SafetyProbability.Low => 1,
                SafetyProbability.Medium => 2,
                SafetyProbability.High => 3,
                _ => -1
            };
        }
    }
}