using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.ExampleConsole;

/// <summary>
/// Advanced examples demonstrating sophisticated usage patterns
/// </summary>
public class AdvancedExamples
{
    private readonly IGeminiService _gemini;
    private readonly ILogger<AdvancedExamples> _logger;

    public AdvancedExamples(IGeminiService gemini, ILogger<AdvancedExamples> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    /// <summary>
    /// Demonstrates performance optimization techniques
    /// </summary>
    public async Task RunPerformanceOptimizationExamples()
    {
        Console.WriteLine("=== PERFORMANCE OPTIMIZATION EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // 1. Fast model for quick responses
            var startTime = DateTime.UtcNow;
            var fastResponse = await _gemini.GenerateAsync(
                "What is the capital of France?",
                GeminiRequestOptions.Fast());
            var fastDuration = DateTime.UtcNow - startTime;

            Console.WriteLine($"Fast Response ({fastDuration.TotalMilliseconds:F0}ms): {fastResponse.Text()}");
            Console.WriteLine();

            // 2. Optimized token usage
            var tokenOptimizedOptions = new GeminiRequestOptions
            {
                MaxTokens = 50, // Limit tokens for concise responses
                Temperature = 0.1f // Lower temperature for focused responses
            };

            var conciseResponse = await _gemini.GenerateAsync(
                "Briefly explain photosynthesis",
                tokenOptimizedOptions);

            var tokenCount = await _gemini.CountTokensAsync(conciseResponse.Text());
            Console.WriteLine($"Concise Response ({tokenCount.totalTokens} tokens): {conciseResponse.Text()}");
            Console.WriteLine();

            // 3. Batch processing simulation
            var questions = new[]
            {
                "What is AI?",
                "What is ML?",
                "What is DL?"
            };

            Console.WriteLine("Batch Processing:");
            var batchTasks = questions.Select(async question =>
            {
                var response = await _gemini.GenerateAsync(question, GeminiRequestOptions.Fast());
                return new { Question = question, Answer = response.Text() };
            });

            var batchResults = await Task.WhenAll(batchTasks);
            foreach (var result in batchResults)
            {
                Console.WriteLine($"Q: {result.Question}");
                Console.WriteLine($"A: {result.Answer}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in performance optimization examples");
        }

        Console.WriteLine("===============================================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates different content generation patterns
    /// </summary>
    public async Task RunContentGenerationPatterns()
    {
        Console.WriteLine("=== CONTENT GENERATION PATTERNS ===");
        Console.WriteLine();

        try
        {
            // 1. Technical Documentation
            var technicalOptions = new GeminiRequestOptions
            {
                Temperature = 0.2f, // Low temperature for accuracy
                MaxTokens = 300
            };

            var technicalContent = await _gemini.GenerateAsync(
                "Write technical documentation for a REST API endpoint that creates a user account",
                technicalOptions);

            Console.WriteLine("Technical Documentation:");
            Console.WriteLine(technicalContent.Text());
            Console.WriteLine();

            // 2. Creative Content
            var creativeContent = await _gemini.GenerateAsync(
                "Write a product description for a smart home device that can read emotions",
                GeminiRequestOptions.Creative());

            Console.WriteLine("Creative Marketing Content:");
            Console.WriteLine(creativeContent.Text());
            Console.WriteLine();

            // 3. Educational Content
            var educationalOptions = new GeminiRequestOptions
            {
                Temperature = 0.3f,
                MaxTokens = 250
            };

            var educationalContent = await _gemini.GenerateAsync(
                "Explain blockchain technology to a 12-year-old using simple analogies",
                educationalOptions);

            Console.WriteLine("Educational Content:");
            Console.WriteLine(educationalContent.Text());
            Console.WriteLine();

            // 4. Code Generation
            var codeContent = await _gemini.GenerateAsync(
                "Create a C# class for a shopping cart with add, remove, and calculate total methods",
                GeminiRequestOptions.Code());

            Console.WriteLine("Generated Code:");
            Console.WriteLine(codeContent.Text());
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in content generation patterns");
        }

        Console.WriteLine("===============================================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates error handling and recovery patterns
    /// </summary>
    public async Task RunErrorHandlingExamples()
    {
        Console.WriteLine("=== ERROR HANDLING EXAMPLES ===");
        Console.WriteLine();

        try
        {
            // 1. Graceful degradation
            Console.WriteLine("1. Graceful Degradation Pattern:");

            try
            {
                // Attempt with preferred options
                var response = await _gemini.GenerateAsync(
                    "Write a detailed analysis",
                    new GeminiRequestOptions { MaxTokens = 1000 });

                Console.WriteLine("SUCCESS: Full response generated successfully");
                Console.WriteLine($"Response length: {response.Text().Length} characters");
            }
            catch (Exception)
            {
                // Fallback to simpler request
                Console.WriteLine("FALLBACK: Falling back to simpler request...");
                var fallbackResponse = await _gemini.GenerateAsync(
                    "Write a brief analysis",
                    GeminiRequestOptions.Fast());

                Console.WriteLine("SUCCESS: Fallback response generated");
                Console.WriteLine($"Fallback response: {fallbackResponse.Text()}");
            }
            Console.WriteLine();

            // 2. Retry with exponential backoff (conceptual)
            Console.WriteLine("2. Retry Pattern (conceptual):");
            var maxRetries = 3;
            var retryDelay = TimeSpan.FromSeconds(1);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _gemini.GenerateAsync("Hello, world!");
                    Console.WriteLine($"SUCCESS: Success on attempt {attempt}");
                    break;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    Console.WriteLine($"RETRY: Attempt {attempt} failed: {ex.Message}");
                    Console.WriteLine($"Waiting {retryDelay.TotalSeconds} seconds before retry...");
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2); // Exponential backoff
                }
            }
            Console.WriteLine();

            // 3. Input validation
            Console.WriteLine("3. Input Validation:");

            var testInputs = new[]
            {
                "", // Empty
                "Valid input",
                new string('A', 10000), // Too long
            };

            foreach (var input in testInputs)
            {
                try
                {
                    ValidationUtilities.ValidateTextInput(input, "test", 1000);
                    Console.WriteLine($"VALID: Input valid: \"{(input.Length > 20 ? input[..20] + "..." : input)}\"");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"INVALID: Input invalid: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in error handling examples");
        }

        Console.WriteLine("===============================================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates integration patterns for real applications
    /// </summary>
    public async Task RunIntegrationPatterns()
    {
        Console.WriteLine("=== INTEGRATION PATTERNS ===");
        Console.WriteLine();

        try
        {
            // 1. Content summarization pipeline
            Console.WriteLine("1. Content Summarization Pipeline:");

            var longContent = @"
            Artificial Intelligence (AI) represents one of the most significant technological
            advancements of our time. It encompasses machine learning, deep learning, natural
            language processing, computer vision, and robotics. AI systems can now perform
            tasks that traditionally required human intelligence, such as recognizing speech,
            making decisions, and solving complex problems. The applications are vast, ranging
            from autonomous vehicles and medical diagnosis to financial trading and customer
            service automation. However, with great power comes great responsibility, and the
            development of AI must be guided by ethical principles to ensure it benefits
            humanity while minimizing potential risks.";

            // Step 1: Count tokens in original content
            var originalTokens = await _gemini.CountTokensAsync(longContent);
            Console.WriteLine($"Original content: {originalTokens.totalTokens} tokens");

            // Step 2: Generate summary
            var summary = await _gemini.GenerateAsync(
                $"Summarize the following text in 2-3 sentences:\n\n{longContent}",
                new GeminiRequestOptions { Temperature = 0.3f, MaxTokens = 100 });

            // Step 3: Count tokens in summary
            var summaryTokens = await _gemini.CountTokensAsync(summary.Text());
            Console.WriteLine($"Summary: {summaryTokens.totalTokens} tokens");
            Console.WriteLine($"Compression ratio: {(double)summaryTokens.totalTokens / originalTokens.totalTokens:P1}");
            Console.WriteLine($"Summary: {summary.Text()}");
            Console.WriteLine();

            // 2. Multi-step content generation
            Console.WriteLine("2. Multi-step Content Generation:");

            // Step 1: Generate outline
            var outline = await _gemini.GenerateAsync(
                "Create a brief outline for an article about renewable energy",
                new GeminiRequestOptions { Temperature = 0.4f, MaxTokens = 150 });

            Console.WriteLine("Generated Outline:");
            Console.WriteLine(outline.Text());
            Console.WriteLine();

            // Step 2: Expand one section
            var expandedSection = await _gemini.GenerateAsync(
                $"Based on this outline, write a detailed paragraph about solar energy:\n\n{outline.Text()}",
                new GeminiRequestOptions { Temperature = 0.5f, MaxTokens = 200 });

            Console.WriteLine("Expanded Section:");
            Console.WriteLine(expandedSection.Text());
            Console.WriteLine();

            // 3. Content validation and improvement
            Console.WriteLine("3. Content Validation and Improvement:");

            var draftContent = "AI is good technology. It help many people. Very useful for business.";

            var improvedContent = await _gemini.GenerateAsync(
                $"Improve the grammar and style of this text while keeping the same meaning:\n\n{draftContent}",
                new GeminiRequestOptions { Temperature = 0.2f, MaxTokens = 100 });

            Console.WriteLine($"Original: {draftContent}");
            Console.WriteLine($"Improved: {improvedContent.Text()}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in integration patterns");
        }

        Console.WriteLine("===============================================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates monitoring and analytics patterns
    /// </summary>
    public async Task RunMonitoringExamples()
    {
        Console.WriteLine("=== MONITORING & ANALYTICS EXAMPLES ===");
        Console.WriteLine();

        try
        {
            var requests = new List<(string prompt, DateTime startTime, DateTime endTime, int tokens)>();

            // Simulate multiple requests with timing
            var testPrompts = new[]
            {
                "What is machine learning?",
                "Explain quantum computing",
                "Write a haiku about technology",
                "List benefits of renewable energy"
            };

            foreach (var prompt in testPrompts)
            {
                var startTime = DateTime.UtcNow;
                var response = await _gemini.GenerateAsync(prompt, GeminiRequestOptions.Fast());
                var endTime = DateTime.UtcNow;
                var tokens = await _gemini.CountTokensAsync(response.Text());

                requests.Add((prompt, startTime, endTime, tokens.totalTokens));

                Console.WriteLine($"Request: \"{prompt}\"");
                Console.WriteLine($"Duration: {(endTime - startTime).TotalMilliseconds:F0}ms");
                Console.WriteLine($"Tokens: {tokens.totalTokens}");
                Console.WriteLine($"Response: {response.Text()[..Math.Min(100, response.Text().Length)]}...");
                Console.WriteLine();
            }

            // Analytics summary
            Console.WriteLine("=== ANALYTICS SUMMARY ===");
            var totalDuration = requests.Sum(r => (r.endTime - r.startTime).TotalMilliseconds);
            var totalTokens = requests.Sum(r => r.tokens);
            var avgDuration = totalDuration / requests.Count;
            var avgTokens = totalTokens / requests.Count;

            Console.WriteLine($"Total Requests: {requests.Count}");
            Console.WriteLine($"Total Duration: {totalDuration:F0}ms");
            Console.WriteLine($"Total Tokens: {totalTokens}");
            Console.WriteLine($"Average Duration: {avgDuration:F0}ms");
            Console.WriteLine($"Average Tokens: {avgTokens:F1}");
            Console.WriteLine($"Tokens per Second: {totalTokens / (totalDuration / 1000):F1}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in monitoring examples");
        }

        Console.WriteLine("===============================================================");
        Console.WriteLine();
    }
}