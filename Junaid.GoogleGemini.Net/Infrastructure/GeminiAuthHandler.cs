using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiAuthHandler<TOptions> : DelegatingHandler
        where TOptions : class, IGeminiAuthHttpClientOptions
    {
        private readonly TOptions _options;

        public GeminiAuthHandler(IOptions<TOptions> options)
        {
            _options = options.Value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_options.Credentials != null)
            {
                request.Headers.Add(GeminiConfiguration.Scheme, _options.Credentials.ApiKey);
            }
            else
            {
                var apiKey = Environment.GetEnvironmentVariable("GeminiApiKey");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Add(GeminiConfiguration.Scheme, apiKey);
                }
                else
                {
                    throw new ArgumentNullException($"Your API key is invalid, as it is an empty string. You can double-check your API key from the Google Cloud API Credentials page (https://console.cloud.google.com/apis/credentials).");
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}