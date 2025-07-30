using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Infrastructure.Helpers
{
    /// <summary>
    /// DEPRECATED: Use ConfigurationUtilities instead
    /// Helper class for migrating from legacy configuration to modern configuration
    /// </summary>
    [Obsolete("This class is deprecated. Use ConfigurationUtilities instead. Will be removed in v7.0.0")]
    public static class ConfigurationMigrationHelper
    {
        /// <summary>
        /// DEPRECATED: Use ConfigurationUtilities.ToGeminiOptions() instead
        /// </summary>
        [Obsolete("Use ConfigurationUtilities.ToGeminiOptions() instead")]
        public static GeminiOptions ToGeminiOptions(this GeminiConfiguration legacyConfig)
        {
            return ConfigurationUtilities.ToGeminiOptions(legacyConfig);
        }

        /// <summary>
        /// DEPRECATED: Use ConfigurationUtilities.ToGeminiRequestOptions() instead
        /// </summary>
        [Obsolete("Use ConfigurationUtilities.ToGeminiRequestOptions() instead")]
        public static GeminiRequestOptions ToGeminiRequestOptions(this GenerateContentConfiguration legacyConfig)
        {
            return ConfigurationUtilities.ToGeminiRequestOptions(legacyConfig);
        }

        /// <summary>
        /// DEPRECATED: Use ConfigurationUtilities.GetMigrationGuide() instead
        /// </summary>
        [Obsolete("Use ConfigurationUtilities.GetMigrationGuide() instead")]
        public static string GetMigrationExample()
        {
            return ConfigurationUtilities.GetMigrationGuide();
        }
    }
}