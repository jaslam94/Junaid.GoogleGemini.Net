using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGeminiServices(
            this IServiceCollection services,
            Action<GeminiConfiguration> configureOptions)
        {
            // Configure options
            var config = new GeminiConfiguration();
            configureOptions(config);
            services.AddSingleton<IGeminiConfiguration>(config);

            // Register HttpClient
            services.AddHttpClient<IGeminiClient, GeminiClient>((serviceProvider, client) =>
            {
                var configuration = serviceProvider.GetRequiredService<IGeminiConfiguration>();
                client.DefaultRequestHeaders.Add(GeminiConfiguration.Scheme, configuration.ApiKey);
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1/");
            });

            // Register services
            services.AddScoped<ITextService, TextService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IVisionService, VisionService>();
            services.AddScoped<IEmbeddingService, EmbeddingService>();
            services.AddScoped<IModelInfoService, ModelInfoService>();

            return services;
        }

        public static IServiceCollection AddGeminiServices(
            this IServiceCollection services,
            IConfiguration configuration,
            string configurationSection = "Gemini")
        {
            var config = new GeminiConfiguration();
            configuration.GetSection(configurationSection).Bind(config);

            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                throw new InvalidOperationException(
                    $"The {configurationSection} section is missing the ApiKey configuration value.");
            }

            return services.AddGeminiServices(options =>
            {
                options.ApiKey = config.ApiKey;
            });
        }
    }
}
