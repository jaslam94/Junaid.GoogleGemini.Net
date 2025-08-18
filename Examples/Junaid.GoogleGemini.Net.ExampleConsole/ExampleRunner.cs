using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.ExampleConsole;

/// <summary>
/// Executes individual example demonstrations
/// </summary>
public class ExampleRunner
{
    private const int MaxDescriptionLength = 80;
    private const int MaxPreviewLength = 200;
    private const int MaxDisplayedModels = 5;

    // Common strings to avoid duplication
    private const string SectionSeparator = "===============================================================";

    // Model names for examples
    private const string EmbeddingModel = "text-embedding-004";

    private readonly IGeminiService _gemini;
    private readonly IEmbeddingService _embeddingService;
    private readonly IModelInfoService _modelInfoService;
    private readonly ISafetyService _safetyService;
    private readonly IFunctionService _functionService;
    private readonly AdvancedExamples _advancedExamples;
    private readonly ILogger<ExampleRunner> _logger;

    public ExampleRunner(
        IGeminiService gemini,
        IEmbeddingService embeddingService,
        IModelInfoService modelInfoService,
        ISafetyService safetyService,
        IFunctionService functionService,
        AdvancedExamples advancedExamples,
        ILogger<ExampleRunner> logger)
    {
        _gemini = gemini;
        _embeddingService = embeddingService;
        _modelInfoService = modelInfoService;
        _safetyService = safetyService;
        _functionService = functionService;
        _advancedExamples = advancedExamples;
        _logger = logger;
    }

    public async Task RunTextGenerationExamplesAsync()
    {
        Console.WriteLine("=== BASIC TEXT GENERATION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunExampleAsync("Simple text generation - Haiku about AI", async () =>
            {
                var response = await _gemini.GenerateAsync("Write a haiku about artificial intelligence");
                Console.WriteLine($"AI Haiku:\n{response.Text()}\n");
            });

            await RunExampleAsync("Creative text generation - Robot painting story", async () =>
            {
                var response = await _gemini.GenerateAsync(
                    "Write a creative short story about a robot learning to paint",
                    GeminiRequestOptions.Creative());
                Console.WriteLine($"Creative Story:\n{response.Text()}\n");
            });

            await RunExampleAsync("Factual text generation - Quantum computing explanation", async () =>
            {
                var response = await _gemini.GenerateAsync(
                    "Explain quantum computing in simple terms",
                    GeminiRequestOptions.Factual());
                Console.WriteLine($"Quantum Computing Explanation:\n{response.Text()}\n");
            });
        }
        catch (Exception ex) when (IsRateLimitException(ex))
        {
            HandleRateLimitException(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in text generation examples");
            throw;
        }

        DisplaySectionEnd();
    }

    public async Task RunModelInfoExamplesAsync()
    {
        Console.WriteLine("=== MODEL INFORMATION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunExampleAsync("Listing all available models", async () =>
            {
                var models = await _modelInfoService.ListModelsAsync();
                Console.WriteLine("Available Models:");
                foreach (var model in models.models.Take(MaxDisplayedModels))
                {
                    Console.WriteLine($"  - {model.name} (Version: {model.version})");
                    var description = TruncateText(model.description, MaxDescriptionLength);
                    Console.WriteLine($"    Description: {description}...");
                    Console.WriteLine($"    Input Token Limit: {model.inputTokenLimit:N0}");
                    Console.WriteLine($"    Output Token Limit: {model.outputTokenLimit:N0}");
                    Console.WriteLine();
                }
            });

            await RunExampleAsync("Getting specific model information (gemini-1.5-pro)", async () =>
            {
                var modelInfo = await _modelInfoService.GetModelAsync(GeminiConstants.Models.Gemini15Pro);
                Console.WriteLine($"Model Details for {modelInfo.name}:");
                Console.WriteLine($"  Display Name: {modelInfo.displayName}");
                Console.WriteLine($"  Input Token Limit: {modelInfo.inputTokenLimit:N0}");
                Console.WriteLine($"  Output Token Limit: {modelInfo.outputTokenLimit:N0}");
                Console.WriteLine($"  Temperature: {modelInfo.temperature}");
                Console.WriteLine();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in model info examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunRequestOptionsExamplesAsync()
    {
        Console.WriteLine("=== REQUEST OPTIONS EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunRequestOptionExample("Creative options - Moon and Sun dialogue",
                "Create an imaginative dialogue between the moon and the sun",
                GeminiRequestOptions.Creative());

            await RunRequestOptionExample("Fast options - Simple math question",
                "What is 2 + 2?",
                GeminiRequestOptions.Fast());

            await RunRequestOptionExample("Code options - C# factorial method",
                "Write a C# method to calculate the factorial of a number",
                GeminiRequestOptions.Code());

            await RunCustomOptionsExample();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in request options examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunVisionExamplesAsync()
    {
        Console.WriteLine("=== VISION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            var imagePath = await ImageHelper.CreateOrFindSampleImageAsync();

            if (imagePath != null)
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var image = new FileObject(imageBytes, Path.GetFileName(imagePath));

                await RunVisionExample("Basic image analysis",
                    "Describe what you see in this image in detail", image);

                await RunVisionExample("Creative vision analysis - story from image",
                    "Write a creative story inspired by this image", image, GeminiRequestOptions.Creative());

                await RunArtCriticVisionExample(image);
                await RunVisionTokenCountingExample(image);
            }
            else
            {
                Console.WriteLine("No sample image found. Skipping vision examples.");
                Console.WriteLine("   Place a sample image (JPG/PNG) in the sample-images folder to test vision features.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vision examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunChatExamplesAsync()
    {
        Console.WriteLine("=== CHAT EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunChatExample("Simple chat conversation - Ask about writing a poem",
                CreateSimpleChatMessages(),
                null);

            await RunChatExample("Creative chat conversation - Uplifting content",
                CreateCreativeChatMessages(),
                GeminiRequestOptions.Creative());

            await RunChatTokenCountingExample();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunStreamingExamplesAsync()
    {
        Console.WriteLine("=== STREAMING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunStreamingExample("Basic text streaming - Knight quest story",
                "Tell me a story about a brave knight on an epic quest");

            await RunStreamingExample("Creative streaming - AI and humanity poem",
                "Write a futuristic poem about AI and humanity",
                GeminiRequestOptions.Creative());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunTokenCountingExamplesAsync()
    {
        Console.WriteLine("=== TOKEN COUNTING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunTokenCountingVariousLengthsExample();
            await RunTokenEfficiencyExample();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in token counting examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunSafetyExamplesAsync()
    {
        Console.WriteLine("=== SAFETY EXAMPLES ===");
        Console.WriteLine();

        try
        {
            var safetySettings = CreateSafetySettings();
            DisplaySafetySettingsInfo(safetySettings);

            await RunSafetyContentGenerationExample(safetySettings.strictSafety);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in safety examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunEmbeddingExamplesAsync()
    {
        Console.WriteLine("=== EMBEDDING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunEmbeddingGenerationExample();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in embedding examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    public async Task RunConfigurationExamplesAsync()
    {
        Console.WriteLine("=== CONFIGURATION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            DisplayEnvironmentInfo();
            await RunConfigurationUtilitiesExample();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in configuration examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    // Advanced examples delegation
    public async Task RunPerformanceOptimizationExamplesAsync() =>
        await _advancedExamples.RunPerformanceOptimizationExamples();

    public async Task RunContentGenerationPatternsAsync() =>
        await _advancedExamples.RunContentGenerationPatterns();

    public async Task RunErrorHandlingExamplesAsync() =>
        await _advancedExamples.RunErrorHandlingExamples();

    public async Task RunIntegrationPatternsAsync() =>
        await _advancedExamples.RunIntegrationPatterns();

    public async Task RunMonitoringExamplesAsync() =>
        await _advancedExamples.RunMonitoringExamples();

    public async Task RunAdvancedFunctionPatternsAsync() =>
        await _advancedExamples.RunFunctionCallingPatterns();

    public async Task RunFunctionServiceExamplesAsync()
    {
        Console.WriteLine("=== FUNCTION CALLING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            await RunFunctionRegistrationExample();
            await RunSimpleCalculatorExample();
            await RunWeatherServiceExample();
            await RunFunctionListingExample();
            await RunErrorHandlingExample();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in function service examples");
            Console.WriteLine($"Error: {ex.Message}");
        }

        DisplaySectionEnd();
    }

    #region Helper Methods

    private static async Task RunExampleAsync(string description, Func<Task> action)
    {
        Console.WriteLine(description);
        await action();
    }

    private async Task RunRequestOptionExample(string description, string prompt, GeminiRequestOptions options)
    {
        await RunExampleAsync(description, async () =>
        {
            var response = await _gemini.GenerateAsync(prompt, options);
            var optionType = description.Split(' ')[0];
            Console.WriteLine($"{optionType} Response:\n{response.Text()}\n");
        });
    }

    private async Task RunVisionExample(string description, string prompt, FileObject image, GeminiRequestOptions? options = null)
    {
        await RunExampleAsync(description, async () =>
        {
            var response = await _gemini.GenerateWithImageAsync(prompt, image, options);
            var resultType = description.Contains("story") ? "Story" : "Description";
            Console.WriteLine($"Image {resultType}:\n{response.Text()}\n");
        });
    }

    private async Task RunChatExample(string description, MessageObject[] messages, GeminiRequestOptions? options)
    {
        await RunExampleAsync(description, async () =>
        {
            var response = await _gemini.ChatAsync(messages, options);
            Console.WriteLine($"Chat Response:\n{response.Text()}\n");
        });
    }

    private async Task RunStreamingExample(string description, string prompt, GeminiRequestOptions? options = null)
    {
        await RunExampleAsync(description, async () =>
        {
            Console.WriteLine("Streaming Response (Real-time):");
            var streamedContent = new List<string>();

            await _gemini.StreamAsync(prompt, chunk =>
            {
                Console.Write(chunk);
                streamedContent.Add(chunk);
            }, options);

            Console.WriteLine("\n");
            Console.WriteLine($"Total streamed content length: {string.Join("", streamedContent).Length} characters\n");
        });
    }

    private async Task RunCustomOptionsExample()
    {
        await RunExampleAsync("Custom options - Renewable energy discussion", async () =>
        {
            var customOptions = new GeminiRequestOptions
            {
                Temperature = 0.8f,
                MaxTokens = 500,
                TopP = 0.9f,
                TopK = 40
            };
            var response = await _gemini.GenerateAsync("Describe the future of renewable energy", customOptions);
            Console.WriteLine($"Custom Options Response:\n{response.Text()}\n");
        });
    }

    private async Task RunArtCriticVisionExample(FileObject image)
    {
        await RunExampleAsync("Vision with personality prompting - art critic", async () =>
        {
            var artCriticPrompt = "You are a professional art critic with expertise in visual analysis. Provide detailed, sophisticated commentary on images including composition, color theory, artistic techniques, and cultural context. Analyze this image from an art critic's perspective.";

            var response = await _gemini.GenerateWithImageAsync(artCriticPrompt, image);
            Console.WriteLine($"Art Critic Analysis:\n{response.Text()}\n");
        });
    }

    private async Task RunVisionTokenCountingExample(FileObject image)
    {
        await RunExampleAsync("Token counting for vision input", async () =>
        {
            var tokens = await _gemini.CountTokensWithImageAsync("Describe this image", image);
            Console.WriteLine($"Vision Token Count: {tokens.totalTokens}\n");
        });
    }

    private async Task RunChatTokenCountingExample()
    {
        await RunExampleAsync("Token counting for chat conversation", async () =>
        {
            var messages = CreateSimpleChatMessages();
            var tokens = await _gemini.CountTokensChatAsync(messages);
            Console.WriteLine($"Chat Token Count: {tokens.totalTokens}\n");
        });
    }

    private async Task RunTokenCountingVariousLengthsExample()
    {
        await RunExampleAsync("Counting tokens for various text lengths", async () =>
        {
            var testTexts = new[]
            {
                "Hello, world!",
                "This is a longer text that contains multiple sentences. It should have more tokens than the previous example.",
                "Write a comprehensive analysis of machine learning algorithms including supervised learning, unsupervised learning, and reinforcement learning paradigms."
            };

            for (int i = 0; i < testTexts.Length; i++)
            {
                var tokens = await _gemini.CountTokensAsync(testTexts[i]);
                Console.WriteLine($"Text {i + 1} ({testTexts[i].Length} chars): {tokens.totalTokens} tokens");
                var preview = testTexts[i].Length > 50 ? testTexts[i][..50] + "..." : testTexts[i];
                Console.WriteLine($"  Text: \"{preview}\"");
                Console.WriteLine();
            }
        });
    }

    private async Task RunTokenEfficiencyExample()
    {
        await RunExampleAsync("Token efficiency comparison", async () =>
        {
            var sampleText = "Artificial intelligence and machine learning are transforming industries.";
            var tokenCount = await _gemini.CountTokensAsync(sampleText);

            Console.WriteLine($"Sample text: \"{sampleText}\"");
            Console.WriteLine($"Character count: {sampleText.Length}");
            Console.WriteLine($"Token count: {tokenCount.totalTokens}");
            Console.WriteLine($"Characters per token: {(double)sampleText.Length / tokenCount.totalTokens:F2}");
            Console.WriteLine();
        });
    }

    private async Task RunSafetyContentGenerationExample(List<SafetySetting> strictSafety)
    {
        await RunExampleAsync("Generating content with strict safety settings", async () =>
        {
            var safePrompt = "Write a heartwarming story about friendship";
            var safeOptions = new GeminiRequestOptions { SafetySettings = strictSafety };

            var safeResponse = await _gemini.GenerateAsync(safePrompt, safeOptions);
            var preview = TruncateText(safeResponse.Text(), MaxPreviewLength);
            Console.WriteLine($"Safe content generated successfully:");
            Console.WriteLine($"{preview}...\n");

            var safetyAnalysis = _safetyService.AnalyzeSafetyRatings(safeResponse);
            Console.WriteLine("Safety Analysis:");
            foreach (var rating in safetyAnalysis)
            {
                Console.WriteLine($"  {rating.Key}: {rating.Value}");
            }
            Console.WriteLine();
        });
    }

    private async Task RunEmbeddingGenerationExample()
    {
        await RunExampleAsync("Generating embeddings for sample texts", async () =>
        {
            var texts = new[]
            {
                "The quick brown fox jumps over the lazy dog",
                "Machine learning is a subset of artificial intelligence",
                "The weather is beautiful today with clear blue skies",
                "Programming languages like C# and Python are popular"
            };

            var embeddings = new List<(string text, object embedding)>();

            foreach (var text in texts)
            {
                var embedding = await _embeddingService.EmbedContentAsync(EmbeddingModel, text);
                embeddings.Add((text, embedding));

                Console.WriteLine($"Text: \"{text}\"");
                Console.WriteLine($"Embedding generated successfully (dimension: {embedding.embedding?.values?.Length ?? 0})");
                if (embedding.embedding?.values?.Length > 0)
                {
                    var firstValues = string.Join(", ", embedding.embedding.values.Take(5).Select(v => v.ToString("F4")));
                    Console.WriteLine($"First 5 values: [{firstValues}...]");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Generated embeddings for {embeddings.Count} texts");
            Console.WriteLine("Note: In a real application, you could calculate cosine similarity between embeddings");
            Console.WriteLine("to find semantically similar texts.");
        });
    }

    private static async Task RunConfigurationUtilitiesExample()
    {
        await RunExampleAsync("Configuration utilities demonstration", async () =>
        {
            var defaultOptions = ConfigurationUtilities.CreateDefaultOptions();
            var devOptions = ConfigurationUtilities.CreateDevelopmentOptions("dev-key");
            var prodOptions = ConfigurationUtilities.CreateProductionOptions("prod-key");

            Console.WriteLine("Configuration Options:");
            Console.WriteLine($"  Default - Timeout: {defaultOptions.TimeoutSeconds}s, Max Retries: {defaultOptions.MaxRetries}");
            Console.WriteLine($"  Development - Timeout: {devOptions.TimeoutSeconds}s, Max Retries: {devOptions.MaxRetries}");
            Console.WriteLine($"  Production - Timeout: {prodOptions.TimeoutSeconds}s, Max Retries: {prodOptions.MaxRetries}");
            Console.WriteLine();

            var defaultSafetyThresholds = ConfigurationUtilities.GetDefaultSafetyThresholds();
            var safetySettings = ConfigurationUtilities.CreateSafetySettings(defaultSafetyThresholds);

            Console.WriteLine($"Default safety settings configured for {safetySettings.Count} categories:");
            foreach (var setting in safetySettings)
            {
                Console.WriteLine($"  {setting.Category}: {setting.Threshold}");
            }
            Console.WriteLine();
        });
    }

    private static MessageObject[] CreateSimpleChatMessages()
    {
        return new[]
        {
            new MessageObject("user", "Hello! What's your name?"),
            new MessageObject("model", "Hello! I'm Gemini, an AI assistant created by Google."),
            new MessageObject("user", "Can you help me write a poem about the ocean?")
        };
    }

    private static MessageObject[] CreateCreativeChatMessages()
    {
        return new[]
        {
            new MessageObject("user", "I'm feeling down today."),
            new MessageObject("model", "I'm sorry to hear that. Would you like to talk about what's bothering you?"),
            new MessageObject("user", "Write me something uplifting and creative to cheer me up.")
        };
    }

    private (List<SafetySetting> strictSafety, List<SafetySetting> moderateSafety, List<SafetySetting> permissiveSafety) CreateSafetySettings()
    {
        return (
            _safetyService.CreateStrictSafetySettings(),
            _safetyService.CreateModerateSafetySettings(),
            _safetyService.CreatePermissiveSafetySettings()
        );
    }

    private static void DisplaySafetySettingsInfo((List<SafetySetting> strictSafety, List<SafetySetting> moderateSafety, List<SafetySetting> permissiveSafety) settings)
    {
        Console.WriteLine($"Strict safety settings: {settings.strictSafety.Count} categories configured");
        Console.WriteLine($"Moderate safety settings: {settings.moderateSafety.Count} categories configured");
        Console.WriteLine($"Permissive safety settings: {settings.permissiveSafety.Count} categories configured");
        Console.WriteLine();
    }

    private static void DisplayEnvironmentInfo()
    {
        var envApiKey = ConfigurationUtilities.GetApiKeyFromEnvironment();
        Console.WriteLine($"API Key from environment: {(string.IsNullOrEmpty(envApiKey) ? "Not set" : "Found (hidden for security)")}");

        if (!string.IsNullOrEmpty(envApiKey))
        {
            var isValidFormat = ConfigurationUtilities.IsValidApiKeyFormat(envApiKey);
            Console.WriteLine($"API Key format valid: {isValidFormat}");
        }
        Console.WriteLine();
    }

    private static void DisplaySectionEnd()
    {
        Console.WriteLine(SectionSeparator);
        Console.WriteLine();
    }

    private static bool IsRateLimitException(Exception ex) =>
        ex.Message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase);

    private void HandleRateLimitException(Exception ex)
    {
        _logger.LogError(ex, "Rate limit exceeded in text generation examples");
        Console.WriteLine();
        Console.WriteLine("RATE LIMIT EXCEEDED");
        Console.WriteLine("   This usually means:");
        Console.WriteLine("   - You've hit the free tier limits");
        Console.WriteLine("   - Need to upgrade to paid plan");
        Console.WriteLine("   - Wait longer between requests");
        Console.WriteLine("   - Check: https://ai.google.dev/gemini-api/docs/rate-limits");
        Console.WriteLine();
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text[..Math.Min(maxLength, text.Length)];
    }

    #endregion Helper Methods

    #region Function Examples Helper Methods

    private async Task RunFunctionRegistrationExample()
    {
        await RunExampleAsync("Function registration and basic concepts", async () =>
        {
            Console.WriteLine("Registering sample functions with the Function Service:");
            Console.WriteLine();

            try
            {
                // Register a simple math function
                var addFunction = CreateAdditionFunction();
                if (!_functionService.HasFunction(addFunction.Name))
                {
                    _functionService.RegisterFunction(addFunction, AdditionHandler);
                    Console.WriteLine($" Registered function: {addFunction.Name}");
                    Console.WriteLine($"  Description: {addFunction.Description}");
                }
                else
                {
                    Console.WriteLine($" Function already registered: {addFunction.Name}");
                }

                // Register a greeting function
                var greetFunction = CreateGreetingFunction();
                if (!_functionService.HasFunction(greetFunction.Name))
                {
                    _functionService.RegisterFunction(greetFunction, GreetingHandler);
                    Console.WriteLine($" Registered function: {greetFunction.Name}");
                    Console.WriteLine($"  Description: {greetFunction.Description}");
                }
                else
                {
                    Console.WriteLine($" Function already registered: {greetFunction.Name}");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already registered"))
            {
                Console.WriteLine("Note: Some functions were already registered from previous runs");
            }

            Console.WriteLine();
            Console.WriteLine($"Total registered functions: {_functionService.GetAvailableFunctions().Count}");
            Console.WriteLine();

            await Task.Delay(100); // Small delay for demo purposes
        });
    }

    private async Task RunSimpleCalculatorExample()
    {
        await RunExampleAsync("Function calling - Simple calculator", async () =>
        {
            // Test the addition function
            var addCall = new Junaid.GoogleGemini.Net.Models.Functions.FunctionCall
            {
                Name = "add_numbers",
                Arguments = """{"a": 15, "b": 27}"""
            };

            var result = await _functionService.CallFunctionAsync(addCall);
            Console.WriteLine($"Function Call: {addCall.Name}({addCall.Arguments})");
            Console.WriteLine($"Result: {result.Response}");

            if (!string.IsNullOrEmpty(result.Error))
            {
                Console.WriteLine($"Error: {result.Error}");
            }
            Console.WriteLine();
        });
    }

    private async Task RunWeatherServiceExample()
    {
        await RunExampleAsync("Function calling - Weather service simulation", async () =>
        {
            // Register weather function only if not already registered
            var weatherFunction = CreateWeatherFunction();
            if (!_functionService.HasFunction(weatherFunction.Name))
            {
                _functionService.RegisterFunction(weatherFunction, WeatherHandler);
            }

            // Test the weather function
            var weatherCall = new Junaid.GoogleGemini.Net.Models.Functions.FunctionCall
            {
                Name = "get_weather",
                Arguments = """{"location": "San Francisco", "unit": "celsius"}"""
            };

            var result = await _functionService.CallFunctionAsync(weatherCall);
            Console.WriteLine($"Function Call: {weatherCall.Name}({weatherCall.Arguments})");
            Console.WriteLine($"Weather Result: {result.Response}");
            Console.WriteLine();
        });
    }

    private async Task RunFunctionListingExample()
    {
        await RunExampleAsync("Function service management", async () =>
        {
            var functions = _functionService.GetAvailableFunctions();
            Console.WriteLine($"Available functions ({functions.Count}):");

            foreach (var func in functions)
            {
                Console.WriteLine($"  • {func.Name}: {func.Description}");
                Console.WriteLine($"    Parameters: {func.Parameters.Properties.Count} properties");

                if (func.Parameters.Required.Any())
                {
                    Console.WriteLine($"    Required: {string.Join(", ", func.Parameters.Required)}");
                }
            }
            Console.WriteLine();

            // Test function existence
            Console.WriteLine("Function existence checks:");
            Console.WriteLine($"  Has 'add_numbers': {_functionService.HasFunction("add_numbers")}");
            Console.WriteLine($"  Has 'nonexistent_func': {_functionService.HasFunction("nonexistent_func")}");
            Console.WriteLine();

            await Task.Delay(100);
        });
    }

    private async Task RunErrorHandlingExample()
    {
        await RunExampleAsync("Function calling error handling", async () =>
        {
            // Test calling a non-existent function
            var invalidCall = new Junaid.GoogleGemini.Net.Models.Functions.FunctionCall
            {
                Name = "nonexistent_function",
                Arguments = """{"param": "value"}"""
            };

            var result = await _functionService.CallFunctionAsync(invalidCall);
            Console.WriteLine($"Calling non-existent function: {invalidCall.Name}");
            Console.WriteLine($"Result: {result.Response}");
            Console.WriteLine($"Error: {result.Error}");
            Console.WriteLine();

            // Test invalid JSON arguments
            var invalidArgsCall = new Junaid.GoogleGemini.Net.Models.Functions.FunctionCall
            {
                Name = "add_numbers",
                Arguments = """{"a": "not_a_number", "b": 5}""" // Invalid argument type
            };

            var result2 = await _functionService.CallFunctionAsync(invalidArgsCall);
            Console.WriteLine($"Calling with invalid arguments: {invalidArgsCall.Arguments}");
            Console.WriteLine($"Result: {result2.Response}");
            Console.WriteLine($"Error: {result2.Error}");
            Console.WriteLine();
        });
    }

    #endregion Function Examples Helper Methods

    #region Function Definitions and Handlers

    private static Junaid.GoogleGemini.Net.Models.Functions.FunctionDefinition CreateAdditionFunction()
    {
        return new Junaid.GoogleGemini.Net.Models.Functions.FunctionDefinition
        {
            Name = "add_numbers",
            Description = "Adds two numbers together",
            Parameters = new Junaid.GoogleGemini.Net.Models.Functions.FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, Junaid.GoogleGemini.Net.Models.Functions.PropertyDefinition>
                {
                    ["a"] = new() { Type = "number", Description = "First number to add" },
                    ["b"] = new() { Type = "number", Description = "Second number to add" }
                },
                Required = new List<string> { "a", "b" }
            }
        };
    }

    private static Junaid.GoogleGemini.Net.Models.Functions.FunctionDefinition CreateGreetingFunction()
    {
        return new Junaid.GoogleGemini.Net.Models.Functions.FunctionDefinition
        {
            Name = "generate_greeting",
            Description = "Generates a personalized greeting message",
            Parameters = new Junaid.GoogleGemini.Net.Models.Functions.FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, Junaid.GoogleGemini.Net.Models.Functions.PropertyDefinition>
                {
                    ["name"] = new() { Type = "string", Description = "Name of the person to greet" },
                    ["style"] = new()
                    {
                        Type = "string",
                        Description = "Style of greeting (formal, casual, friendly)",
                        Enum = new List<string> { "formal", "casual", "friendly" }
                    }
                },
                Required = new List<string> { "name" }
            }
        };
    }

    private static Junaid.GoogleGemini.Net.Models.Functions.FunctionDefinition CreateWeatherFunction()
    {
        return new Junaid.GoogleGemini.Net.Models.Functions.FunctionDefinition
        {
            Name = "get_weather",
            Description = "Gets current weather information for a location",
            Parameters = new Junaid.GoogleGemini.Net.Models.Functions.FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, Junaid.GoogleGemini.Net.Models.Functions.PropertyDefinition>
                {
                    ["location"] = new() { Type = "string", Description = "The city or location to get weather for" },
                    ["unit"] = new()
                    {
                        Type = "string",
                        Description = "Temperature unit (celsius, fahrenheit)",
                        Enum = new List<string> { "celsius", "fahrenheit" }
                    }
                },
                Required = new List<string> { "location" }
            }
        };
    }

    private static async Task<object> AdditionHandler(Dictionary<string, object> arguments)
    {
        try
        {
            if (!arguments.TryGetValue("a", out var aObj) || !arguments.TryGetValue("b", out var bObj))
            {
                throw new ArgumentException("Missing required parameters 'a' and 'b'");
            }

            // Handle JsonElement objects from System.Text.Json deserialization
            var a = ExtractDoubleValue(aObj, "a");
            var b = ExtractDoubleValue(bObj, "b");
            var result = a + b;

            return new { result = result, operation = "addition", inputs = new { a, b } };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error in addition function: {ex.Message}");
        }
    }

    private static async Task<object> GreetingHandler(Dictionary<string, object> arguments)
    {
        try
        {
            if (!arguments.TryGetValue("name", out var nameObj))
            {
                throw new ArgumentException("Missing required parameter 'name'");
            }

            var name = ExtractStringValue(nameObj, "name");
            var style = arguments.TryGetValue("style", out var styleObj)
                ? ExtractStringValue(styleObj, "style")
                : "friendly";

            var greeting = style?.ToLower() switch
            {
                "formal" => $"Good day, {name}. It is a pleasure to make your acquaintance.",
                "casual" => $"Hey {name}! What's up?",
                "friendly" => $"Hello {name}! It's wonderful to meet you!",
                _ => $"Hi {name}!"
            };

            return new { greeting, style, timestamp = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error in greeting function: {ex.Message}");
        }
    }

    private static async Task<object> WeatherHandler(Dictionary<string, object> arguments)
    {
        try
        {
            if (!arguments.TryGetValue("location", out var locationObj))
            {
                throw new ArgumentException("Missing required parameter 'location'");
            }

            var location = ExtractStringValue(locationObj, "location");
            var unit = arguments.TryGetValue("unit", out var unitObj)
                ? ExtractStringValue(unitObj, "unit")
                : "celsius";

            // Simulate weather data (in a real app, this would call a weather API)
            var random = new Random();
            var temp = unit?.ToLower() == "fahrenheit"
                ? random.Next(32, 100) // Fahrenheit range
                : random.Next(0, 38);   // Celsius range

            var conditions = new[] { "sunny", "cloudy", "rainy", "partly cloudy", "clear" };
            var condition = conditions[random.Next(conditions.Length)];

            return new
            {
                location = location,
                temperature = temp,
                unit = unit,
                condition = condition,
                humidity = random.Next(30, 90),
                windSpeed = random.Next(0, 25),
                timestamp = DateTime.UtcNow,
                source = "Simulated Weather Service"
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error in weather function: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to extract double values from JsonElement or primitive types
    /// </summary>
    private static double ExtractDoubleValue(object value, string paramName)
    {
        return value switch
        {
            System.Text.Json.JsonElement jsonElement => jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.String when double.TryParse(jsonElement.GetString(), out var parsed) => parsed,
                _ => throw new ArgumentException($"Parameter '{paramName}' must be a number, got {jsonElement.ValueKind}")
            },
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s, out var parsed) => parsed,
            _ => throw new ArgumentException($"Parameter '{paramName}' must be a number, got {value?.GetType().Name ?? "null"}")
        };
    }

    /// <summary>
    /// Helper method to extract string values from JsonElement or primitive types
    /// </summary>
    private static string ExtractStringValue(object value, string paramName)
    {
        return value switch
        {
            System.Text.Json.JsonElement jsonElement => jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Number => jsonElement.ToString(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                _ => throw new ArgumentException($"Parameter '{paramName}' cannot be converted to string from {jsonElement.ValueKind}")
            },
            string s => s,
            _ => value?.ToString() ?? throw new ArgumentException($"Parameter '{paramName}' cannot be null")
        };
    }

    #endregion Function Definitions and Handlers
}