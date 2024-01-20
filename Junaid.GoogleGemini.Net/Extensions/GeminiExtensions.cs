using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Extensions
{
    public static class GeminiExtensions
    {
        public static IServiceCollection AddGemini(this IServiceCollection services)
        {
            services.AddTransient<GeminiAuthHandler<GeminiHttpClientOptions>>();

            services.AddHttpClient<GeminiClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiHttpClientOptions>>().Value;
                client.BaseAddress = options.Url;
            })
            .AddHttpMessageHandler<GeminiAuthHandler<GeminiHttpClientOptions>>();

            services.AddTransient<ITextService, TextService>();
            services.AddTransient<IVisionService, VisionService>();
            services.AddTransient<IChatService, ChatService>();
            services.AddTransient<IEmbeddingService, EmbeddingService>();
            services.AddTransient<IModelInfoService, ModelInfoService>();

            return services;
        }
    }
}