namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiConfiguration
    {
        public const string Scheme = "x-goog-api-key";

        private string apiKey;

        public GeminiConfiguration()
        {
            apiKey = string.Empty;
        }

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