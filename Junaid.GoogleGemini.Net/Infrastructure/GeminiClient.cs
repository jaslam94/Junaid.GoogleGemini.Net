using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiClient : IGeminiClient
    {
        private readonly HttpClient _httpClient;

        public GeminiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<TResponse> GetAsync<TResponse>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);
            return await HandleResponse<TResponse>(response);
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            var serializedContent = JsonSerializer.Serialize(data, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var jsonContent = new StringContent(serializedContent, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, jsonContent);
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
                var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(content);
                throw new GeminiException(geminiError, geminiError.error.message);
            }
        }

        public async IAsyncEnumerable<string> SendAsync<TRequest>(string endpoint, TRequest data)
        {
            var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, data, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            ms.Seek(0, SeekOrigin.Begin);

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using (var requestContent = new StreamContent(ms))
            {
                request.Content = requestContent;
                requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
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
                        var geminiError = System.Text.Json.JsonSerializer.Deserialize<ApiErrorResponse>(content);
                        throw new GeminiException(geminiError, geminiError.error.message);
                    }
                }
            }
        }
    }
}