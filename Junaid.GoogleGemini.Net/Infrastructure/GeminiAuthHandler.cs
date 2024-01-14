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

            return await base.SendAsync(request, cancellationToken);
        }
    }
}