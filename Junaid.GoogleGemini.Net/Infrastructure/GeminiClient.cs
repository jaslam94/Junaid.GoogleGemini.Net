using Junaid.GoogleGemini.Net.Models;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiClient : IGeminiClient
    {
        private readonly string ApiKey;

        private readonly HttpClient HttpClient;

        public static string DefaultBaseUri => "https://generativelanguage.googleapis.com";

        public GeminiClient(string apiKey)
        {
            if (apiKey != null && apiKey.Length == 0)
            {
                throw new ArgumentException("API key cannot be the empty string.", nameof(apiKey));
            }
            this.ApiKey = apiKey;
            this.HttpClient = new HttpClient { BaseAddress = new Uri(DefaultBaseUri) };
            this.HttpClient.DefaultRequestHeaders.Add("X-Goog-Api-Key", ApiKey);
        }

        public GeminiClient(HttpClient httpClient)
        {
            this.HttpClient = httpClient;
            if (!this.HttpClient.DefaultRequestHeaders.Contains("X-Goog-Api-Key"))
            {
                this.HttpClient.DefaultRequestHeaders.Add("X-Goog-Api-Key", ApiKey);
            }
        }

        public GeminiClient(HttpClient httpClient, string apiKey)
        {
            if (apiKey != null && apiKey.Length == 0)
            {
                throw new ArgumentException("API key cannot be the empty string.", nameof(apiKey));
            }
            this.ApiKey = apiKey;
            this.HttpClient = httpClient;
            if (!this.HttpClient.DefaultRequestHeaders.Contains("X-Goog-Api-Key"))
            {
                this.HttpClient.DefaultRequestHeaders.Add("X-Goog-Api-Key", this.ApiKey);
            }
        }

        public async Task<TResponse> GetAsync<TResponse>(string endpoint)
        {
            var response = await HttpClient.GetAsync(endpoint);
            return await HandleResponse<TResponse>(response);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            var serializedContent = JsonSerializer.Serialize(data, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var jsonContent = new StringContent(serializedContent, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(endpoint, jsonContent);
            return await HandleResponse<TResponse>(response);
        }

        private async Task<T> HandleResponse<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<T>(content);
            }
            else
            {
                var geminiError = JsonSerializer.Deserialize<ErrorResponse>(content);
                throw new GeminiException(geminiError, geminiError.error.message);
            }
        }

        public async IAsyncEnumerable<string> PostAsync<TRequest>(string endpoint, TRequest data)
        {
            var serializedContent = JsonSerializer.Serialize(data, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var jsonContent = new StringContent(serializedContent, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(endpoint, jsonContent);
            if (response.IsSuccessStatusCode)
            {
                using (var responseStream = await response.Content.ReadAsStreamAsync())
                using (var streamReader = new StreamReader(responseStream))
                {
                    string line = string.Empty;
                    while ((line = await streamReader.ReadLineAsync()) != null)
                    {
                        if (line.Contains(@"""text"""))
                        {
                            var jsonString = "{" + line + "}";
                            var jsonObject = JsonSerializer.Deserialize<JsonObject>(jsonString);
                            yield return jsonObject?["text"]?.ToString();
                        }
                    }
                }
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                var geminiError = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(content);
                throw new GeminiException(geminiError, geminiError.error.message);
            }
        }
    }
}