using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Junaid.GoogleGemini.Net.Extensions
{
    public class FooHttpClientOptions : IBasicAuthHttpClientOptions
    {
        public Uri Url { get; set; }
        public BasicAuthCredential Credentials { get; set; }
    }

    public interface IBasicAuthHttpClientOptions
    {
        Uri Url { get; set; }
        BasicAuthCredential Credentials { get; set; }
    }

    public class BasicAuthCredential
    {
        public const string Scheme = "x-goog-api-key";
        public string ApiKey { get; set; } = string.Empty;
    }

    public static class GeminiExtensions
    {
        public static IServiceCollection AddGemini<THttpClient, THttpClientOptions>(
            this IServiceCollection services,
            Action<THttpClientOptions> configure) where THttpClient : class where THttpClientOptions : class, IBasicAuthHttpClientOptions
        {
            services
                .AddOptions<THttpClientOptions>()
                .Configure(configure)
                .Validate(options => string.IsNullOrEmpty(options.Credentials.ApiKey)
                                     || string.IsNullOrWhiteSpace(options.Credentials.ApiKey), GeminiExceptionMessages.InvalidApiKeyMessage)
                .ValidateOnStart();

            services.AddTransient<BasicAuthenticationHandler<THttpClientOptions>>();

            services.AddHttpClient<THttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<THttpClientOptions>>().Value;
                client.BaseAddress = options.Url;
            })
            .AddHttpMessageHandler<BasicAuthenticationHandler<THttpClientOptions>>();

            return services;
        }
    }

    public class BasicAuthenticationHandler<TOptions> : DelegatingHandler
        where TOptions : class, IBasicAuthHttpClientOptions
    {
        private readonly TOptions _options;

        public BasicAuthenticationHandler(IOptions<TOptions> options)
        {
            _options = options.Value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(BasicAuthCredential.Scheme, _options.Credentials.ApiKey);

            return await base.SendAsync(request, cancellationToken);
        }
    }

    public class GeminiExceptionMessages
    {
        public const string InvalidApiKeyMessage = "Your API key is invalid, as it is an empty string. You can double-check your API key from the Google Cloud API Credentials page (https://console.cloud.google.com/apis/credentials).";
    }
}