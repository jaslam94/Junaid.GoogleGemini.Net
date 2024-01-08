using Junaid.GoogleGemini.Net.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Extensions
{
    public static class GeminiExtensions
    {
        public static IServiceCollection AddGemini(this IServiceCollection services, IOptionsSnapshot<GeminiConfiguration> options)
        {
            var geminiConfiguration = options.Value;
            var apiKey = geminiConfiguration.ApiKey;
            var httpClient = geminiConfiguration.HttpClient;

            services.AddTransient<IGeminiClient, GeminiClient>(provider =>
            {
                return new GeminiClient(options);
            });

            return services;
        }
    }
}