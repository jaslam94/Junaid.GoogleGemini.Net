using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
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
        /// Adds Gemini services to the service collection with configuration section
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration section containing Gemini options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddGemini(this IServiceCollection services, IConfigurationSection configuration)
        {
            return services.AddGemini(options => configuration.Bind(options));
        }

        /// <summary>
        /// Adds Gemini services to the service collection with configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration containing Gemini options</param>
        /// <param name="sectionName">The section name to bind from (default: "Gemini")</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddGemini(this IServiceCollection services, IConfiguration configuration, string sectionName = "Gemini")
        {
            var section = configuration.GetSection(sectionName);
            return services.AddGemini(options => section.Bind(options));
        }

        /// <summary>
        /// Adds Gemini services to the service collection with options configuration
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

            // Register rate limiter
            services.AddSingleton<IRateLimiter>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
                return new GeminiRateLimiter(options.RateLimit);
            });

            // Configure HTTP client with authentication
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

                // Configure proxy if specified
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

            // Register authentication handler
            services.AddTransient<GeminiAuthHandler>();
            
            // Register IGeminiClient interface with GeminiClient implementation
            services.AddTransient<IGeminiClient>(serviceProvider => 
                serviceProvider.GetRequiredService<GeminiClient>());
            
            // Register unified service (replaces multiple specialized services)
            services.AddTransient<IGeminiService, GeminiService>();
            
            // Keep specialized services that have unique functionality
            services.AddTransient<IModelInfoService, ModelInfoService>();
            services.AddTransient<IEmbeddingService, EmbeddingService>();
            services.AddTransient<ISafetyService, SafetyService>();
            services.AddSingleton<IFunctionService, FunctionService>();

            return services;
        }
    }
}