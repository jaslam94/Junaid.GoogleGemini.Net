using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Extensions
{
    public static class GeminiExtensions
    {
        public static IServiceCollection AddGemini<THttpClient, THttpClientOptions>(
            this IServiceCollection services,
            Action<THttpClientOptions> configure) where THttpClient : class where THttpClientOptions : class, IGeminiAuthHttpClientOptions
        {
            services
                .AddOptions<THttpClientOptions>()
                .Configure(configure);

            services.AddTransient<GeminiAuthHandler<THttpClientOptions>>();

            services.AddHttpClient<THttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<THttpClientOptions>>().Value;
                client.BaseAddress = options.Url;
            })
            .AddHttpMessageHandler<GeminiAuthHandler<THttpClientOptions>>();

            services.AddTransient<ITextService, TextService>();

            return services;
        }
    }
}