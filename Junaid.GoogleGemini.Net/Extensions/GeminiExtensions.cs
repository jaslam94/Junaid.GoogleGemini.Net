using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
#if NET8_0_OR_GREATER
using Microsoft.Extensions.Http.Resilience;
using Polly;
#endif

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
            => services.AddGemini(configureOptions, configurePipeline: null);

        /// <summary>
        /// Adds Gemini services, letting the caller prepend handlers to the HTTP pipeline.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Action to configure options</param>
        /// <param name="configurePipeline">
        /// Invoked before any built-in handler is registered, so handlers added here become the
        /// <b>outermost</b> ones — they see the request before authentication is applied and can
        /// short-circuit the call without touching auth, resilience or the network. This is the seam
        /// used by <c>Junaid.GoogleGemini.Net.Testing</c> to record and replay HTTP cassettes, and it
        /// is why a recorded cassette can never contain the API key.
        /// </param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddGemini(
            this IServiceCollection services,
            Action<GeminiOptions> configureOptions,
            Action<IHttpClientBuilder>? configurePipeline)
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
            var clientBuilder = services.AddHttpClient<GeminiClient>((sp, client) =>
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
            });

            // Caller-supplied handlers go on FIRST, making them the outermost layer — they can
            // short-circuit before auth ever attaches the API key. See the overload's docs.
            configurePipeline?.Invoke(clientBuilder);

            // Auth is registered next => outermost of the built-in handlers, so it runs once and
            // adds the API key a single time. The retry handler is INNER, so it re-sends the network
            // request without re-running auth (the buffered request is safe to resend).
            clientBuilder.AddHttpMessageHandler<GeminiAuthHandler>();

#if NET8_0_OR_GREATER
            // net8+: the battle-tested standard resilience handler (retry + backoff + jitter).
            clientBuilder.AddResilienceHandler("gemini", static (builder, context) =>
            {
                var resilienceOptions = context.ServiceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    // Defaults already retry on 5xx, 408, 429, HttpRequestException and timeouts.
                    MaxRetryAttempts = resilienceOptions.MaxRetries,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = resilienceOptions.RetryBaseDelay,
                });
            });
#else
            // netstandard2.0: Microsoft.Extensions.Http.Resilience isn't available, so use our own.
            clientBuilder.AddHttpMessageHandler(sp =>
            {
                var resilienceOptions = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                return new GeminiRetryHandler(resilienceOptions.MaxRetries, resilienceOptions.RetryBaseDelay);
            });
#endif

            // Dedicated client for the Files API (host root + auth; no retry, since a partially
            // completed upload isn't safe to blindly replay).
            services.AddHttpClient(GeminiHttpClients.Files, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.GetLeftPart(UriPartial.Authority) + "/");
                client.Timeout = TimeSpan.FromMinutes(5); // uploads of large media can be slow
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
            services.AddTransient<IFileService, FileService>();
            services.AddTransient<ICachingService, CachingService>();
            services.AddTransient<ISafetyService, SafetyService>();
            services.AddSingleton<IFunctionService, FunctionService>();

            return services;
        }
    }
}