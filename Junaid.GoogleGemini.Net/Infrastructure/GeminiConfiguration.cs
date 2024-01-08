using Junaid.GoogleGemini.Net.Exceptions;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiConfiguration
    {
        private string apiKey;
        private HttpClient httpClient;

        public GeminiConfiguration()
        {
            apiKey = string.Empty;
            httpClient = new HttpClient();
        }

        public string ApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GeminiApiKey")))
                    {
                        apiKey = Environment.GetEnvironmentVariable("GeminiApiKey") ?? string.Empty;
                    }
                }
                return apiKey;
            }

            set
            {
                if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new GeminiException($"Your API key is invalid, as it is an empty string. You can double-check your API key from the Google Cloud API Credentials page (https://console.cloud.google.com/apis/credentials).");
                }
                apiKey = value;
            }
        }

        public HttpClient HttpClient
        {
            get
            {
                if (httpClient == null)
                {
                    httpClient = new HttpClient();
                    httpClient = new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com") };
                    httpClient.DefaultRequestHeaders.Add("X-Goog-Api-Key", apiKey);
                }
                return httpClient;
            }

            set
            {
                if (!value.DefaultRequestHeaders.Contains("X-Goog-Api-Key"))
                {
                    value.DefaultRequestHeaders.Add("X-Goog-Api-Key", apiKey);
                }

                httpClient = value;
            }
        }
    }
}