using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.ExampleConsole;

static class Program
{
    static async Task Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Create host with dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register Gemini services with configuration
                services.AddGemini(configuration.GetSection("Gemini"));
                
                // Register the main application service
                services.AddTransient<GeminiExampleApp>();
                services.AddTransient<AdvancedExamples>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();

        // Run the application
        var app = host.Services.GetRequiredService<GeminiExampleApp>();
        await app.RunAsync();
    }
}

/// <summary>
/// Main application class demonstrating all Gemini.Net functionality
/// </summary>
public class GeminiExampleApp
{
    private readonly IGeminiService _gemini;
    private readonly IEmbeddingService _embeddingService;
    private readonly IModelInfoService _modelInfoService;
    private readonly ISafetyService _safetyService;
    private readonly IFunctionService _functionService;
    private readonly AdvancedExamples _advancedExamples;
    private readonly ILogger<GeminiExampleApp> _logger;

    public GeminiExampleApp(
        IGeminiService gemini,
        IEmbeddingService embeddingService,
        IModelInfoService modelInfoService,
        ISafetyService safetyService,
        IFunctionService functionService,
        AdvancedExamples advancedExamples,
        ILogger<GeminiExampleApp> logger)
    {
        _gemini = gemini;
        _embeddingService = embeddingService;
        _modelInfoService = modelInfoService;
        _safetyService = safetyService;
        _functionService = functionService;
        _advancedExamples = advancedExamples;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("?? Starting Junaid.GoogleGemini.Net Example Console Application");
        
        try
        {
            // Check if API key is configured
            if (!await ValidateApiKey())
            {
                _logger.LogError("? API key validation failed. Please check your configuration.");
                Console.WriteLine();
                Console.WriteLine("?? Setup Instructions:");
                Console.WriteLine("1. Get your API key from: https://makersuite.google.com/app/apikey");
                Console.WriteLine("2. Set environment variable: set GeminiApiKey=your-key-here");
                Console.WriteLine("3. Or update appsettings.json with your API key");
                return;
            }

            // Display menu and run examples
            await ShowWelcomeMessage();
            await RunAllExamples();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? An unexpected error occurred");
        }
        
        _logger.LogInformation("? Example application completed");
        
        Console.WriteLine();
        Console.WriteLine("Thank you for exploring Junaid.GoogleGemini.Net!");
        Console.WriteLine("Visit https://github.com/jaslam94/Junaid.GoogleGemini.Net for more information.");
    }

    private async Task<bool> ValidateApiKey()
    {
        try
        {
            // Try to get available models to validate API key
            var models = await _modelInfoService.ListModelsAsync();
            _logger.LogInformation("? API key validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? API key validation failed");
            return false;
        }
    }

    private async Task ShowWelcomeMessage()
    {
        Console.WriteLine("?????????????????????????????????????????????????????????????????");
        Console.WriteLine("?                  Junaid.GoogleGemini.Net                      ?");
        Console.WriteLine("?                    Example Console App                        ?");
        Console.WriteLine("?                        v7.0.0                                ?");
        Console.WriteLine("?????????????????????????????????????????????????????????????????");
        Console.WriteLine();
        
        _logger.LogInformation("This example demonstrates all features of the Junaid.GoogleGemini.Net library");
        Console.WriteLine();
        
        // Show what will be demonstrated
        Console.WriteLine("?? Features to be demonstrated:");
        Console.WriteLine("   • Basic text generation with various options");
        Console.WriteLine("   • Vision capabilities with image analysis");
        Console.WriteLine("   • Chat conversations and streaming");
        Console.WriteLine("   • Token counting and optimization");
        Console.WriteLine("   • Safety settings and content analysis");
        Console.WriteLine("   • Model information and selection");
        Console.WriteLine("   • Configuration and utilities");
        Console.WriteLine("   • Advanced integration patterns");
        Console.WriteLine("   • Performance monitoring");
        Console.WriteLine();
    }

    private async Task RunAllExamples()
    {
        // Basic Examples
        await RunTextGenerationExamples();
        await RunModelInfoExamples();
        await RunRequestOptionsExamples();
        await RunVisionExamples();
        await RunChatExamples();
        await RunStreamingExamples();
        await RunTokenCountingExamples();
        await RunSafetyExamples();
        await RunEmbeddingExamples();
        await RunConfigurationExamples();
        await RunFunctionServiceExamples();
        
        // Advanced Examples
        Console.WriteLine("?? === ADVANCED EXAMPLES ===");
        Console.WriteLine();
        await _advancedExamples.RunPerformanceOptimizationExamples();
        await _advancedExamples.RunContentGenerationPatterns();
        await _advancedExamples.RunErrorHandlingExamples();
        await _advancedExamples.RunIntegrationPatterns();
        await _advancedExamples.RunMonitoringExamples();
    }

    #region 1. Basic Text Generation Examples
    
    private async Task RunTextGenerationExamples()
    {
        Console.WriteLine("?? === BASIC TEXT GENERATION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Simple text generation
            _logger.LogInformation("1. Simple text generation...");
            var response1 = await _gemini.GenerateAsync("Write a haiku about artificial intelligence");
            Console.WriteLine($"AI Haiku:\n{response1.Text()}\n");

            // Text generation with creative options
            _logger.LogInformation("2. Creative text generation...");
            var response2 = await _gemini.GenerateAsync(
                "Write a creative short story about a robot learning to paint",
                GeminiRequestOptions.Creative());
            Console.WriteLine($"Creative Story:\n{response2.Text()}\n");

            // Text generation with factual options
            _logger.LogInformation("3. Factual text generation...");
            var response3 = await _gemini.GenerateAsync(
                "Explain quantum computing in simple terms",
                GeminiRequestOptions.Factual());
            Console.WriteLine($"Quantum Computing Explanation:\n{response3.Text()}\n");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in text generation examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 2. Model Information Examples
    
    private async Task RunModelInfoExamples()
    {
        Console.WriteLine("?? === MODEL INFORMATION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // List all available models
            _logger.LogInformation("1. Listing available models...");
            var models = await _modelInfoService.ListModelsAsync();
            Console.WriteLine("Available Models:");
            foreach (var model in models.models.Take(5)) // Show first 5 to avoid cluttering
            {
                Console.WriteLine($"  - {model.name} (Version: {model.version})");
                Console.WriteLine($"    Description: {model.description?[..Math.Min(80, model.description?.Length ?? 0)]}...");
                Console.WriteLine($"    Input Token Limit: {model.inputTokenLimit:N0}");
                Console.WriteLine($"    Output Token Limit: {model.outputTokenLimit:N0}");
                Console.WriteLine();
            }

            // Get specific model information
            _logger.LogInformation("2. Getting specific model information...");
            var modelInfo = await _modelInfoService.GetModelAsync("gemini-1.5-pro");
            Console.WriteLine($"Model Details for {modelInfo.name}:");
            Console.WriteLine($"  Display Name: {modelInfo.displayName}");
            Console.WriteLine($"  Input Token Limit: {modelInfo.inputTokenLimit:N0}");
            Console.WriteLine($"  Output Token Limit: {modelInfo.outputTokenLimit:N0}");
            Console.WriteLine($"  Temperature: {modelInfo.temperature}");
            Console.WriteLine();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in model info examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 3. Request Options Examples
    
    private async Task RunRequestOptionsExamples()
    {
        Console.WriteLine("?? === REQUEST OPTIONS EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Using different predefined options
            _logger.LogInformation("1. Using predefined Creative options...");
            var creativeResponse = await _gemini.GenerateAsync(
                "Create an imaginative dialogue between the moon and the sun",
                GeminiRequestOptions.Creative());
            Console.WriteLine($"Creative Response:\n{creativeResponse.Text()}\n");

            // Using Fast options for quick responses
            _logger.LogInformation("2. Using Fast options...");
            var fastResponse = await _gemini.GenerateAsync(
                "What is 2 + 2?",
                GeminiRequestOptions.Fast());
            Console.WriteLine($"Fast Response:\n{fastResponse.Text()}\n");

            // Using Code options for code generation
            _logger.LogInformation("3. Using Code options...");
            var codeResponse = await _gemini.GenerateAsync(
                "Write a C# method to calculate the factorial of a number",
                GeminiRequestOptions.Code());
            Console.WriteLine($"Code Response:\n{codeResponse.Text()}\n");

            // Custom options with specific parameters
            _logger.LogInformation("4. Using custom options...");
            var customOptions = new GeminiRequestOptions
            {
                Temperature = 0.8f,
                MaxTokens = 500,
                TopP = 0.9f,
                TopK = 40
            };
            var customResponse = await _gemini.GenerateAsync(
                "Describe the future of renewable energy",
                customOptions);
            Console.WriteLine($"Custom Options Response:\n{customResponse.Text()}\n");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in request options examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 4. Vision Examples
    
    private async Task RunVisionExamples()
    {
        Console.WriteLine("??? === VISION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Create a simple test image if it doesn't exist
            var imagePath = await CreateOrFindSampleImage();
            
            if (imagePath != null)
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var image = new FileObject(imageBytes, Path.GetFileName(imagePath));

                // Basic vision example
                _logger.LogInformation("1. Basic image analysis...");
                var visionResponse = await _gemini.GenerateWithImageAsync(
                    "Describe what you see in this image in detail",
                    image);
                Console.WriteLine($"Image Description:\n{visionResponse.Text()}\n");

                // Vision with specific options
                _logger.LogInformation("2. Vision with creative analysis...");
                var creativeVisionResponse = await _gemini.GenerateWithImageAsync(
                    "Write a creative story inspired by this image",
                    image,
                    GeminiRequestOptions.Creative());
                Console.WriteLine($"Creative Image Story:\n{creativeVisionResponse.Text()}\n");

                // Count tokens for vision input
                _logger.LogInformation("3. Counting tokens for vision input...");
                var visionTokens = await _gemini.CountTokensWithImageAsync(
                    "Describe this image",
                    image);
                Console.WriteLine($"Vision Token Count: {visionTokens.totalTokens}\n");
            }
            else
            {
                Console.WriteLine("?? No sample image found. Skipping vision examples.");
                Console.WriteLine("   Place a sample image (JPG/PNG) in the sample-images folder to test vision features.");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vision examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    private async Task<string?> CreateOrFindSampleImage()
    {
        var sampleImagesDir = Path.Combine(Directory.GetCurrentDirectory(), "sample-images");
        
        if (!Directory.Exists(sampleImagesDir))
        {
            Directory.CreateDirectory(sampleImagesDir);
        }

        // Look for existing images
        var imageExtensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.webp" };
        foreach (var extension in imageExtensions)
        {
            var files = Directory.GetFiles(sampleImagesDir, extension);
            if (files.Length > 0)
            {
                return files[0];
            }
        }

        // Create a simple test image programmatically (basic bitmap)
        try
        {
            var testImagePath = Path.Combine(sampleImagesDir, "test-image.png");
            await CreateSimpleTestImage(testImagePath);
            return testImagePath;
        }
        catch
        {
            return null;
        }
    }

    private async Task CreateSimpleTestImage(string path)
    {
        // Create a simple 1x1 PNG image for testing
        // This is a minimal PNG file in base64
        var simplePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        var imageBytes = Convert.FromBase64String(simplePngBase64);
        await File.WriteAllBytesAsync(path, imageBytes);
    }

    #endregion

    #region 5. Chat Examples
    
    private async Task RunChatExamples()
    {
        Console.WriteLine("?? === CHAT EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Simple chat conversation
            _logger.LogInformation("1. Simple chat conversation...");
            var messages = new[]
            {
                new MessageObject("user", "Hello! What's your name?"),
                new MessageObject("model", "Hello! I'm Gemini, an AI assistant created by Google."),
                new MessageObject("user", "Can you help me write a poem about the ocean?")
            };

            var chatResponse = await _gemini.ChatAsync(messages);
            Console.WriteLine($"Chat Response:\n{chatResponse.Text()}\n");

            // Chat with creative options
            _logger.LogInformation("2. Creative chat conversation...");
            var creativeMessages = new[]
            {
                new MessageObject("user", "I'm feeling down today."),
                new MessageObject("model", "I'm sorry to hear that. Would you like to talk about what's bothering you?"),
                new MessageObject("user", "Write me something uplifting and creative to cheer me up.")
            };

            var creativeChatResponse = await _gemini.ChatAsync(
                creativeMessages,
                GeminiRequestOptions.Creative());
            Console.WriteLine($"Creative Chat Response:\n{creativeChatResponse.Text()}\n");

            // Count tokens for chat
            _logger.LogInformation("3. Counting tokens for chat...");
            var chatTokens = await _gemini.CountTokensChatAsync(messages);
            Console.WriteLine($"Chat Token Count: {chatTokens.totalTokens}\n");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 6. Streaming Examples
    
    private async Task RunStreamingExamples()
    {
        Console.WriteLine("?? === STREAMING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Basic text streaming
            _logger.LogInformation("1. Basic text streaming...");
            Console.WriteLine("Streaming Response (Real-time):");
            var streamedContent = new List<string>();
            
            await _gemini.StreamAsync(
                "Tell me a story about a brave knight on an epic quest",
                chunk =>
                {
                    Console.Write(chunk);
                    streamedContent.Add(chunk);
                });
            
            Console.WriteLine("\n");
            Console.WriteLine($"Total streamed content length: {string.Join("", streamedContent).Length} characters\n");

            // Streaming with options
            _logger.LogInformation("2. Streaming with creative options...");
            Console.WriteLine("Creative Streaming Response:");
            var creativeStreamContent = new List<string>();
            
            await _gemini.StreamAsync(
                "Write a futuristic poem about AI and humanity",
                chunk =>
                {
                    Console.Write(chunk);
                    creativeStreamContent.Add(chunk);
                },
                GeminiRequestOptions.Creative());
            
            Console.WriteLine("\n");
            Console.WriteLine($"Creative stream content length: {string.Join("", creativeStreamContent).Length} characters\n");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 7. Token Counting Examples
    
    private async Task RunTokenCountingExamples()
    {
        Console.WriteLine("?? === TOKEN COUNTING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Count tokens for different types of content
            var testTexts = new[]
            {
                "Hello, world!",
                "This is a longer text that contains multiple sentences. It should have more tokens than the previous example.",
                "Write a comprehensive analysis of machine learning algorithms including supervised learning, unsupervised learning, and reinforcement learning paradigms."
            };

            _logger.LogInformation("1. Counting tokens for various text lengths...");
            for (int i = 0; i < testTexts.Length; i++)
            {
                var tokens = await _gemini.CountTokensAsync(testTexts[i]);
                Console.WriteLine($"Text {i + 1} ({testTexts[i].Length} chars): {tokens.totalTokens} tokens");
                Console.WriteLine($"  Text: \"{(testTexts[i].Length > 50 ? testTexts[i][..50] + "..." : testTexts[i])}\"");
                Console.WriteLine();
            }

            // Compare token counts for different models (if available)
            _logger.LogInformation("2. Comparing token efficiency...");
            var sampleText = "Artificial intelligence and machine learning are transforming industries.";
            var tokenCount = await _gemini.CountTokensAsync(sampleText);
            
            Console.WriteLine($"Sample text: \"{sampleText}\"");
            Console.WriteLine($"Character count: {sampleText.Length}");
            Console.WriteLine($"Token count: {tokenCount.totalTokens}");
            Console.WriteLine($"Characters per token: {(double)sampleText.Length / tokenCount.totalTokens:F2}");
            Console.WriteLine();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in token counting examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 8. Safety Examples
    
    private async Task RunSafetyExamples()
    {
        Console.WriteLine("??? === SAFETY EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Create different safety settings
            _logger.LogInformation("1. Creating different safety configurations...");
            
            var strictSafety = _safetyService.CreateStrictSafetySettings();
            var moderateSafety = _safetyService.CreateModerateSafetySettings();
            var permissiveSafety = _safetyService.CreatePermissiveSafetySettings();

            Console.WriteLine($"Strict safety settings: {strictSafety.Count} categories configured");
            Console.WriteLine($"Moderate safety settings: {moderateSafety.Count} categories configured");
            Console.WriteLine($"Permissive safety settings: {permissiveSafety.Count} categories configured");
            Console.WriteLine();

            // Test content with different safety levels
            _logger.LogInformation("2. Testing content generation with safety settings...");
            
            var safePrompt = "Write a heartwarming story about friendship";
            
            // Generate with strict safety
            var safeOptions = new GeminiRequestOptions
            {
                SafetySettings = strictSafety
            };
            
            var safeResponse = await _gemini.GenerateAsync(safePrompt, safeOptions);
            Console.WriteLine($"Safe content generated successfully:");
            Console.WriteLine($"{safeResponse.Text()[..Math.Min(200, safeResponse.Text().Length)]}...\n");

            // Analyze safety ratings
            _logger.LogInformation("3. Analyzing safety ratings...");
            var safetyAnalysis = _safetyService.AnalyzeSafetyRatings(safeResponse);
            
            Console.WriteLine("Safety Analysis:");
            foreach (var rating in safetyAnalysis)
            {
                Console.WriteLine($"  {rating.Key}: {rating.Value}");
            }
            Console.WriteLine();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in safety examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 9. Embedding Examples
    
    private async Task RunEmbeddingExamples()
    {
        Console.WriteLine("?? === EMBEDDING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // Generate embeddings for different texts
            _logger.LogInformation("1. Generating embeddings for sample texts...");
            
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
                var embedding = await _embeddingService.EmbedContentAsync("text-embedding-004", text);
                embeddings.Add((text, embedding));
                
                Console.WriteLine($"Text: \"{text}\"");
                Console.WriteLine($"Embedding generated successfully (dimension: {embedding.embedding?.values?.Length ?? 0})");
                if (embedding.embedding?.values?.Length > 0)
                {
                    Console.WriteLine($"First 5 values: [{string.Join(", ", embedding.embedding.values.Take(5).Select(v => v.ToString("F4")))}...]");
                }
                Console.WriteLine();
            }

            // Demonstrate embedding similarity (conceptual)
            _logger.LogInformation("2. Embedding analysis complete");
            Console.WriteLine($"Generated embeddings for {embeddings.Count} texts");
            Console.WriteLine("Note: In a real application, you could calculate cosine similarity between embeddings");
            Console.WriteLine("to find semantically similar texts.");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in embedding examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 10. Configuration Examples
    
    private async Task RunConfigurationExamples()
    {
        Console.WriteLine("?? === CONFIGURATION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            _logger.LogInformation("1. Demonstrating configuration utilities...");

            // Show environment variable support
            var envApiKey = ConfigurationUtilities.GetApiKeyFromEnvironment();
            Console.WriteLine($"API Key from environment: {(string.IsNullOrEmpty(envApiKey) ? "Not set" : "Found (hidden for security)")}");

            // Validate API key format
            if (!string.IsNullOrEmpty(envApiKey))
            {
                var isValidFormat = ConfigurationUtilities.IsValidApiKeyFormat(envApiKey);
                Console.WriteLine($"API Key format valid: {isValidFormat}");
            }
            Console.WriteLine();

            // Show different configuration options
            _logger.LogInformation("2. Different configuration approaches...");
            
            var defaultOptions = ConfigurationUtilities.CreateDefaultOptions();
            var devOptions = ConfigurationUtilities.CreateDevelopmentOptions("dev-key");
            var prodOptions = ConfigurationUtilities.CreateProductionOptions("prod-key");

            Console.WriteLine("Configuration Options:");
            Console.WriteLine($"  Default - Timeout: {defaultOptions.TimeoutSeconds}s, Max Retries: {defaultOptions.MaxRetries}");
            Console.WriteLine($"  Development - Timeout: {devOptions.TimeoutSeconds}s, Max Retries: {devOptions.MaxRetries}");
            Console.WriteLine($"  Production - Timeout: {prodOptions.TimeoutSeconds}s, Max Retries: {prodOptions.MaxRetries}");
            Console.WriteLine();

            // Show safety settings configuration
            _logger.LogInformation("3. Safety settings configuration...");
            
            var defaultSafetyThresholds = ConfigurationUtilities.GetDefaultSafetyThresholds();
            var safetySettings = ConfigurationUtilities.CreateSafetySettings(defaultSafetyThresholds);
            
            Console.WriteLine($"Default safety settings configured for {safetySettings.Count} categories:");
            foreach (var setting in safetySettings)
            {
                Console.WriteLine($"  {setting.Category}: {setting.Threshold}");
            }
            Console.WriteLine();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in configuration examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion

    #region 11. Function Service Examples
    
    private async Task RunFunctionServiceExamples()
    {
        Console.WriteLine("?? === FUNCTION SERVICE EXAMPLES ===");
        Console.WriteLine();

        try
        {
            _logger.LogInformation("1. Function service capabilities...");
            
            Console.WriteLine("Function Service Features:");
            Console.WriteLine("  - Register custom functions for tool use");
            Console.WriteLine("  - Call functions from Gemini responses");
            Console.WriteLine("  - Handle structured function calls");  
            Console.WriteLine("  - Integrate with external APIs and services");
            Console.WriteLine();
            
            Console.WriteLine("Example function registration pattern:");
            Console.WriteLine("  functionService.RegisterFunction(weatherFunction, weatherHandler);");
            Console.WriteLine("  var result = await functionService.CallFunctionAsync(functionCall);");
            Console.WriteLine();
            
            Console.WriteLine("Note: Complete function examples require defining:");
            Console.WriteLine("  - Function schemas (parameters, descriptions)");
            Console.WriteLine("  - Function handlers (actual implementation)");
            Console.WriteLine("  - Integration with Gemini function calling");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in function service examples");
        }

        Console.WriteLine("?????????????????????????????????????????????????????????????");
        Console.WriteLine();
    }

    #endregion
}