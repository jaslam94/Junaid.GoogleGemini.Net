using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    /// <summary>
    /// Legacy configuration class - DEPRECATED
    /// Use GeminiOptions with AddGemini() extension method instead
    /// </summary>
    [Obsolete("This class is deprecated. Use GeminiOptions with AddGemini() extension method instead. Will be removed in v7.0.0")]
    public class GeminiConfiguration : IGeminiConfiguration
    {
        /// <summary>
        /// DEPRECATED: Use GeminiAuthHandler instead
        /// </summary>
        [Obsolete("Use GeminiAuthHandler for authentication instead")]
        public const string Scheme = "x-goog-api-key";

        private string apiKey;

        /// <summary>
        /// DEPRECATED: Use GeminiOptions instead
        /// </summary>
        [Obsolete("Use GeminiOptions instead")]
        public GeminiConfiguration()
        {
            apiKey = string.Empty;
        }

        /// <summary>
        /// DEPRECATED: Use GeminiOptions.ApiKey instead
        /// </summary>
        [Obsolete("Use GeminiOptions.ApiKey instead")]
        public string ApiKey
        {
            get
            {
                return apiKey;
            }

            set
            {
                if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException(nameof(ApiKey), $"Your API key is invalid, as it is an empty string. You can double-check your API key from the Google Cloud API Credentials page (https://console.cloud.google.com/apis/credentials).");
                }
                apiKey = value;
            }
        }
    }
}