﻿using Junaid.GoogleGemini.Net.Models;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using System.Text;
using System.Text.Json;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiClient : IGeminiClient
    {
        private readonly string ApiKey;

        private readonly HttpClient HttpClient;

        public static string DefaultBaseUri => "https://generativelanguage.googleapis.com";
        public string BaseUri { get; }

        public GeminiClient(string apiKey, string baseUri)
        {
            if (apiKey != null && apiKey.Length == 0)
            {
                throw new ArgumentException("API key cannot be the empty string.", nameof(apiKey));
            }

            this.ApiKey = apiKey;
            this.BaseUri = baseUri ?? DefaultBaseUri;

            this.HttpClient = new HttpClient { BaseAddress = new Uri(BaseUri) };
            this.HttpClient.DefaultRequestHeaders.Add("X-Goog-Api-Key", ApiKey);
        }

        public async Task<TResponse> GetAsync<TResponse>(string endpoint)
        {
            var response = await HttpClient.GetAsync(endpoint);
            return await HandleResponse<TResponse>(response);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            var jsonContent = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

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
    }
}