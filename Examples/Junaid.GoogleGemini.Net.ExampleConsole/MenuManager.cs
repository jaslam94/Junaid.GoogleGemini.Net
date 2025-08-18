using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.ExampleConsole;

/// <summary>
/// Manages the interactive menu system for the example application
/// </summary>
public class MenuManager
{
    private readonly ExampleRunner _exampleRunner;
    private readonly ILogger<MenuManager> _logger;

    public MenuManager(ExampleRunner exampleRunner, ILogger<MenuManager> logger)
    {
        _exampleRunner = exampleRunner;
        _logger = logger;
    }

    public async Task RunInteractiveMenuAsync()
    {
        var examples = GetMainMenuOptions();

        while (true)
        {
            DisplayMainMenu(examples);
            var choice = ReadUserChoice();

            try
            {
                if (await HandleMenuChoiceAsync(choice, examples))
                    return; // Exit requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running example: {Choice}", choice);
                DisplayError(ex.Message);
                WaitForKeyPress("Press any key to return to menu...");
                Console.Clear();
                ShowWelcomeMessage();
            }
        }
    }

    public async Task RunAdvancedMenuAsync()
    {
        var advancedExamples = GetAdvancedMenuOptions();

        while (true)
        {
            DisplayAdvancedMenu(advancedExamples);
            var choice = ReadUserChoice();

            switch (choice)
            {
                case "b":
                case "back":
                case "main":
                    Console.Clear();
                    ShowWelcomeMessage();
                    return;

                case "q":
                case "quit":
                case "exit":
                    DisplayGoodbye();
                    Environment.Exit(0);
                    break;

                case "":
                    DisplayInvalidChoice();
                    await DelayAsync();
                    continue;

                default:
                    if (await ExecuteAdvancedExampleAsync(choice, advancedExamples))
                    {
                        WaitForKeyPress("Example completed! Press any key to continue...");
                    }
                    else
                    {
                        DisplayInvalidChoice(choice);
                        await DelayAsync();
                    }
                    break;
            }
        }
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
        Console.WriteLine("   - System instructions for behavior control");
        Console.WriteLine("   - Vision capabilities with image analysis");
        Console.WriteLine("   - Chat conversations and streaming");
        Console.WriteLine("   - Token counting and optimization");
        Console.WriteLine("   - Safety settings and content analysis");
        Console.WriteLine("   - Model information and selection");
        Console.WriteLine("   - Configuration and utilities");
        Console.WriteLine("   - Advanced integration patterns");
        Console.WriteLine();

        Console.WriteLine("RATE LIMIT NOTICE:");
        Console.WriteLine("   Free tier: ~15 requests/minute, 1,500/day");
        Console.WriteLine("   Paid tier: Higher limits available");
        Console.WriteLine("   More info: https://ai.google.dev/gemini-api/docs/rate-limits");
        Console.WriteLine();
    }

    private Dictionary<string, (string Description, Func<Task> Action)> GetMainMenuOptions()
    {
        return new Dictionary<string, (string Description, Func<Task> Action)>
        {
            ["1"] = ("Text Generation Examples", _exampleRunner.RunTextGenerationExamplesAsync),
            ["2"] = ("Model Information Examples", _exampleRunner.RunModelInfoExamplesAsync),
            ["3"] = ("Request Options Examples", _exampleRunner.RunRequestOptionsExamplesAsync),
            ["4"] = ("Vision Examples", _exampleRunner.RunVisionExamplesAsync),
            ["5"] = ("Chat Examples", _exampleRunner.RunChatExamplesAsync),
            ["6"] = ("Streaming Examples", _exampleRunner.RunStreamingExamplesAsync),
            ["7"] = ("Token Counting Examples", _exampleRunner.RunTokenCountingExamplesAsync),
            ["8"] = ("Safety Examples", _exampleRunner.RunSafetyExamplesAsync),
            ["9"] = ("Embedding Examples", _exampleRunner.RunEmbeddingExamplesAsync),
            ["10"] = ("Configuration Examples", _exampleRunner.RunConfigurationExamplesAsync),
            ["11"] = ("Function Calling Examples", _exampleRunner.RunFunctionServiceExamplesAsync),
            ["a"] = ("Advanced Examples Menu", RunAdvancedMenuAsync)
        };
    }

    private Dictionary<string, (string Description, Func<Task> Action)> GetAdvancedMenuOptions()
    {
        return new Dictionary<string, (string Description, Func<Task> Action)>
        {
            ["1"] = ("Performance Optimization", _exampleRunner.RunPerformanceOptimizationExamplesAsync),
            ["2"] = ("Content Generation Patterns", _exampleRunner.RunContentGenerationPatternsAsync),
            ["3"] = ("Error Handling Examples", _exampleRunner.RunErrorHandlingExamplesAsync),
            ["4"] = ("Integration Patterns", _exampleRunner.RunIntegrationPatternsAsync),
            ["5"] = ("Monitoring Examples", _exampleRunner.RunMonitoringExamplesAsync),
            ["6"] = ("Function Calling Patterns", () => _exampleRunner.RunAdvancedFunctionPatternsAsync())
        };
    }

    private static void DisplayMainMenu(Dictionary<string, (string Description, Func<Task> Action)> examples)
    {
        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine("                          MAIN MENU                        ");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        Console.WriteLine("BASIC EXAMPLES:");
        foreach (var example in examples.Where(x => char.IsDigit(x.Key[0])).Take(6))
        {
            Console.WriteLine($"  [{example.Key}] {example.Value.Description}");
        }
        Console.WriteLine();

        Console.WriteLine("SPECIALIZED EXAMPLES:");
        foreach (var example in examples.Where(x => char.IsDigit(x.Key[0])).Skip(6))
        {
            Console.WriteLine($"  [{example.Key}] {example.Value.Description}");
        }
        Console.WriteLine();

        Console.WriteLine("ADVANCED:");
        foreach (var example in examples.Where(x => !char.IsDigit(x.Key[0])))
        {
            Console.WriteLine($"  [{example.Key}] {example.Value.Description}");
        }
        Console.WriteLine();

        Console.WriteLine("CONTROLS:");
        Console.WriteLine("  [r] Refresh/Return to main menu");
        Console.WriteLine("  [q] Quit application");
        Console.WriteLine();
    }

    private static void DisplayAdvancedMenu(Dictionary<string, (string Description, Func<Task> Action)> examples)
    {
        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine("                    ADVANCED EXAMPLES MENU                  ");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        foreach (var example in examples)
        {
            Console.WriteLine($"  [{example.Key}] {example.Value.Description}");
        }
        Console.WriteLine();

        Console.WriteLine("CONTROLS:");
        Console.WriteLine("  [b] Back to main menu");
        Console.WriteLine("  [q] Quit application");
        Console.WriteLine();
    }

    private static string ReadUserChoice()
    {
        Console.Write("Enter your choice: ");
        return Console.ReadLine()?.Trim().ToLower() ?? string.Empty;
    }

    private static async Task<bool> HandleMenuChoiceAsync(string choice, Dictionary<string, (string Description, Func<Task> Action)> examples)
    {
        switch (choice)
        {
            case "q":
            case "quit":
            case "exit":
                DisplayGoodbye();
                return true;

            case "r":
            case "refresh":
            case "menu":
                Console.Clear();
                ShowWelcomeMessage();
                return false;

            case "":
                DisplayInvalidChoice();
                await DelayAsync();
                return false;

            default:
                return await ExecuteSelectedExampleAsync(choice, examples);
        }
    }

    private static async Task<bool> ExecuteSelectedExampleAsync(string choice, Dictionary<string, (string Description, Func<Task> Action)> examples)
    {
        if (!examples.TryGetValue(choice, out var example))
        {
            DisplayInvalidChoice(choice);
            await DelayAsync();
            return false;
        }

        Console.WriteLine();
        Console.WriteLine($"Running: {example.Description}");
        Console.WriteLine("=".PadRight(60, '='));

        await example.Action();

        Console.WriteLine();
        WaitForKeyPress("Example completed! Press any key to return to menu...");
        Console.Clear();
        ShowWelcomeMessage();
        return false;
    }

    private async Task<bool> ExecuteAdvancedExampleAsync(string choice, Dictionary<string, (string Description, Func<Task> Action)> examples)
    {
        if (!examples.TryGetValue(choice, out var example))
            return false;

        Console.WriteLine();
        Console.WriteLine($"Running: {example.Description}");
        Console.WriteLine("=".PadRight(60, '='));

        try
        {
            await example.Action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running advanced example: {Choice}", choice);
            DisplayError(ex.Message);
        }

        return true;
    }

    private static void DisplayGoodbye()
    {
        Console.WriteLine("Goodbye! Thanks for trying Junaid.GoogleGemini.Net!");
    }

    private static void DisplayInvalidChoice(string? choice = null)
    {
        if (string.IsNullOrEmpty(choice))
        {
            Console.WriteLine("Please enter a valid choice.");
        }
        else
        {
            Console.WriteLine($"Invalid choice: '{choice}'. Please try again.");
        }
    }

    private static void DisplayError(string message)
    {
        Console.WriteLine();
        Console.WriteLine("An error occurred while running the example:");
        Console.WriteLine($"   {message}");
        Console.WriteLine();
    }

    private static void WaitForKeyPress(string message)
    {
        Console.WriteLine(message);
        Console.ReadKey();
    }

    private static async Task DelayAsync() => await Task.Delay(1500);
}