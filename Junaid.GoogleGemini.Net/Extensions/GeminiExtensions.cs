using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        public static IServiceCollection AddGemini(this IServiceCollection services, Action<GeminiOptions> configureOptions) =>
            services.AddGemini(configureOptions, configurePipeline: null);

        /// <summary>
        /// Adds Gemini services to the service collection with options configuration, plus a hook to
        /// customize the <see cref="GeminiClient"/> <see cref="HttpClient"/> pipeline directly — e.g. to
        /// insert a test/replay handler (see <c>Junaid.GoogleGemini.Net.Testing</c>'s
        /// <c>AddCassette</c>/<c>AddGeminiWithCassette</c>).
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Action to configure options</param>
        /// <param name="configurePipeline">
        /// Invoked with the <see cref="GeminiClient"/>'s <see cref="IHttpClientBuilder"/> after the
        /// primary handler is configured but BEFORE <see cref="GeminiAuthHandler"/> is added — so any
        /// handler added here becomes the OUTERMOST handler on the pipeline, running before
        /// authentication. Pass <c>null</c> to skip (equivalent to the single-parameter overload).
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

            // Register cost governor (cumulative daily budget enforcement + cost observability).
            // options.Budget == null (the default) is handled inside GeminiCostGovernor as a
            // zero-overhead no-op — see its constructor docs.
            services.AddSingleton<ICostGovernor>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
                var logger = serviceProvider.GetRequiredService<ILogger<GeminiCostGovernor>>();
                return new GeminiCostGovernor(options.Budget, GeminiTelemetry.RecordCost, logger);
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

            // Let the caller insert handlers that must run BEFORE authentication (e.g. a cassette
            // record/replay handler, which must never see or need the API key). Anything added here
            // becomes the OUTERMOST handler, ahead of GeminiAuthHandler below.
            configurePipeline?.Invoke(clientBuilder);

            // Auth handler is added next => (absent a configurePipeline hook) it is the OUTERMOST
            // handler, so it runs once and adds the API key a single time. The retry handler is INNER,
            // so it re-sends the network request without re-running auth (the buffered request is safe
            // to resend).
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