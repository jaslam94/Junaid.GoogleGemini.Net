using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.ExampleConsole;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var configuration = BuildConfiguration();
        var host = CreateHost(configuration);

        var app = host.Services.GetRequiredService<GeminiExampleApp>();
        await app.RunAsync();
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static IHost CreateHost(IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddGemini(configuration.GetSection("Gemini"));
                services.AddTransient<GeminiExampleApp>();
                services.AddTransient<ExampleRunner>();
                services.AddTransient<MenuManager>();
                services.AddTransient<AdvancedExamples>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();
    }
}

/// <summary>
/// Main application class for the Gemini.Net example console application
/// </summary>
public class GeminiExampleApp
{
    private readonly IModelInfoService _modelInfoService;
    private readonly MenuManager _menuManager;
    private readonly ILogger<GeminiExampleApp> _logger;

    public GeminiExampleApp(
        IModelInfoService modelInfoService,
        MenuManager menuManager,
        ILogger<GeminiExampleApp> logger)
    {
        _modelInfoService = modelInfoService;
        _menuManager = menuManager;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Junaid.GoogleGemini.Net Example Console Application");

        try
        {
            if (!await ValidateApiKeyAsync())
            {
                DisplayApiKeyInstructions();
                return;
            }

            ShowWelcomeMessage();
            await _menuManager.RunInteractiveMenuAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred in example application");
        }

        _logger.LogInformation("Example application completed");
        DisplayClosingMessage();
    }

    public static void ShowWelcomeMessage()
    {
        Console.WriteLine("================================================================");
        Console.WriteLine("                  Junaid.GoogleGemini.Net                       ");
        Console.WriteLine("                    Example Console App                         ");
        Console.WriteLine("                        v5.1.0                                 ");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        Console.WriteLine("CHOOSE YOUR ADVENTURE!");
        Console.WriteLine("=============================");
        Console.WriteLine("Select specific examples to run based on your interests.");
        Console.WriteLine("No more sequential execution - pick what you want to explore!");
        Console.WriteLine();

        Console.WriteLine("FEATURES AVAILABLE:");
        Console.WriteLine("   - Basic text generation with various options");
        Console.WriteLine("   - Vision capabilities with image analysis");
        Console.WriteLine("   - Chat conversations and streaming");
        Console.WriteLine("   - Token counting and optimization");
        Console.WriteLine("   - Safety settings and content analysis");
        Console.WriteLine("   - Model information and selection");
        Console.WriteLine("   - Configuration and utilities");
        Console.WriteLine("   - Function calling and tool integration");
        Console.WriteLine("   - Advanced integration patterns");
        Console.WriteLine();

        Console.WriteLine("RATE LIMIT NOTICE:");
        Console.WriteLine("   Free tier: ~15 requests/minute, 1,500/day");
        Console.WriteLine("   Paid tier: Higher limits available");
        Console.WriteLine("   More info: https://ai.google.dev/gemini-api/docs/rate-limits");
        Console.WriteLine();
    }

    private async Task<bool> ValidateApiKeyAsync()
    {
        try
        {
            await _modelInfoService.ListModelsAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API key validation failed");
            return false;
        }
    }

    private static void DisplayApiKeyInstructions()
    {
        Console.WriteLine();
        Console.WriteLine("Setup Instructions:");
        Console.WriteLine("1. Get your API key from: https://makersuite.google.com/app/apikey");
        Console.WriteLine("2. Set environment variable: set GeminiApiKey=your-key-here");
        Console.WriteLine("3. Or update appsettings.json with your API key");
    }

    private static void DisplayClosingMessage()
    {
        Console.WriteLine();
        Console.WriteLine("Thank you for exploring Junaid.GoogleGemini.Net!");
        Console.WriteLine("Visit https://github.com/jaslam94/Junaid.GoogleGemini.Net for more information.");
    }
}