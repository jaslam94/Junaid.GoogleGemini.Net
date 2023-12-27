using Junaid.GoogleGemini.Net.Models;
using System.Configuration;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiConfiguration
    {
        private static string apiKey;
        private static IGeminiClient geminiClient;

        public static string ApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["GeminiApiKey"]))
                    {
                        apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
                    }
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GeminiApiKey")))
                    {
                        apiKey = Environment.GetEnvironmentVariable("GeminiApiKey");
                    }
                }

                return apiKey;
            }

            set
            {
                if (value != apiKey)
                {
                    GeminiClient = null;
                }

                apiKey = value;
            }
        }

        public static IGeminiClient GeminiClient
        {
            get
            {
                if (geminiClient == null)
                {
                    geminiClient = BuildDefaultGeminiClient();
                }

                return geminiClient;
            }

            set => geminiClient = value;
        }

        private static GeminiClient BuildDefaultGeminiClient()
        {
            if (ApiKey != null && ApiKey.Length == 0)
            {
                var message = $"Your API key is invalid, as it is an empty string. You can double-check your API key from the Google Cloud API Credentials page (https://console.cloud.google.com/apis/credentials).";
                throw new GeminiException(message);
            }

            return new GeminiClient(ApiKey);
        }
    }
}