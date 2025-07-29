using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace Junaid.GoogleGemini.Net.Extensions
{
    /// <summary>
    /// Extension methods for configuring Gemini services
    /// </summary>
    public static class GeminiExtensions
    {
        /// <summary>
        /// Adds Gemini services to the service collection with configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration section containing Gemini options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddGemini(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddGemini(options => configuration.Bind(options));
            return services;
        }

        /// <summary>
        /// Adds Gemini services to the service collection with options
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Action to configure options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddGemini(this IServiceCollection services, Action<GeminiOptions> configureOptions)
        {
            // Configure and validate options
            services.AddOptions<GeminiOptions>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .Services
                .AddSingleton<IValidateOptions<GeminiOptions>, GeminiOptionsValidator>();

            // Configure HTTP client
            services.AddHttpClient<GeminiClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = options.BaseUrl;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                var handler = new HttpClientHandler();

                if (options.Proxy != null)
                {
                    var proxy = new WebProxy(options.Proxy.Address);
                    if (!string.IsNullOrEmpty(options.Proxy.Username))
                    {
                        proxy.Credentials = new NetworkCredential(
                            options.Proxy.Username,
                            options.Proxy.Password);
                    }
                    proxy.BypassProxyOnLocal = options.Proxy.BypassOnLocal;
                    handler.Proxy = proxy;
                    handler.UseProxy = true;
                }

                return handler;
            })
            .AddHttpMessageHandler<GeminiAuthHandler>();

            // Register core services
            services.AddTransient<GeminiAuthHandler>();
            services.AddSingleton<IRateLimiter>(_ => 
            {
                var options = _.GetRequiredService<IOptions<GeminiOptions>>().Value;
                return new GeminiRateLimiter(
                    options.RateLimit.Enabled,
                    options.RateLimit.RequestsPerMinute,
                    options.RateLimit.TokensPerMinute);
            });

            // Register feature services
            services.AddTransient<ITextService, TextService>();
            services.AddTransient<IVisionService, VisionService>();
            services.AddTransient<IChatService, ChatService>();
            services.AddTransient<IModelInfoService, ModelInfoService>();
            services.AddTransient<IEmbeddingService, EmbeddingService>();
            services.AddTransient<ICodeService, CodeService>();
            services.AddTransient<ISafetyService, SafetyService>();
            services.AddSingleton<IFunctionService, FunctionService>();

            return services;
        }
    }
}