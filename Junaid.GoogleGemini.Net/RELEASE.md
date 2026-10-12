# Release Notes

## v5.1.0
- Function calling: Complete implementation that handles JSON parameters correctly
- JsonElement fix: Fixed deserialization errors when calling functions with numeric parameters
- Interactive menu: The example console app has a new "CHOOSE YOUR ADVENTURE" menu. You can pick which features to try.
- Function examples: Added demonstrations for a calculator, a weather service, and error handling
- Developer experience: Added helper methods for safe parameter extraction and for preventing duplicate registration

Example of function calling:
```csharp
// Register a function
var weatherFunction = new FunctionDefinition
{
    Name = "get_weather",
    Description = "Gets weather for a location",
    Parameters = new FunctionParameters
    {
        Properties = new Dictionary<string, PropertyDefinition>
        {
            ["location"] = new() { Type = "string", Description = "City name" },
            ["unit"] = new() { Type = "string", Enum = new[] { "celsius", "fahrenheit" } }
        },
        Required = new[] { "location" }
    }
};

functionService.RegisterFunction(weatherFunction, async (args) =>
{
    var location = ExtractStringValue(args["location"], "location");
    var unit = args.TryGetValue("unit", out var u) ? ExtractStringValue(u, "unit") : "celsius";
    return new { weather = "sunny", temperature = 25, unit, location };
});

// Call the function
var result = await functionService.CallFunctionAsync(new FunctionCall
{
    Name = "get_weather",
    Arguments = """{"location": "San Francisco", "unit": "celsius"}"""
});
```

## v5.0.0
- Cleanup: Removed all legacy and obsolete services to simplify the architecture
- Removed services: TextService, VisionService, and ChatService
- Removed legacy code: ServiceCollectionExtensions and GeminiConfiguration classes
- Unified service: Single `IGeminiService` for all content generation operations (text, vision, chat, streaming)
- Specialized services: Reduced to 4 services: `IEmbeddingService`, `IModelInfoService`, `ISafetyService`, `IFunctionService`
- Request options: Added predefined `GeminiRequestOptions` (Creative, Fast, Code, Factual)
- Model constants: Added `GeminiConstants.Models` to make model selection easy (Gemini25Pro, Gemini25Flash, etc.)
- Configuration: Added a `GeminiOptions` class that holds all settings
- Environment variables: Automatic API key loading from `GeminiApiKey` environment variable
- Rate limiting: Added built-in rate limiting with `System.Threading.RateLimiting`
- Resilience: Updated the Polly integration (v8.2.0) to improve retry policies
- Safety: Added predefined safety settings (Strict, Moderate, Permissive)
- Utilities: Updated `ConfigurationUtilities`, `ValidationUtilities`, `FileUtilities`, and `GeminiConstants`
- .NET 8 LTS only: The library targets .NET 8 exclusively, for performance
- Streaming: Improved streaming across all operations
- Token counting: Added token counting for text, vision, and chat inputs
- Proxy support: Added proxy configuration options

Example of unified API:
```csharp
// All operations through single service
var response = await gemini.GenerateAsync("Your prompt");
var visionResponse = await gemini.GenerateWithImageAsync("Describe image", image);
var chatResponse = await gemini.ChatAsync(messages);

// Predefined options
var creative = await gemini.GenerateAsync("Write a poem", GeminiRequestOptions.Creative());
var code = await gemini.GenerateAsync("Write a function", GeminiRequestOptions.Code());
```

## v4.0.0
- Using Dependency Injection.
- Using Typed HttpClient.
- Changed the way services are configured and consumed.

## v3.2.0
Added Model Info service and Embedding service.

## v3.1.1
Internal refactoring for the stream content method to reduce memory consumption and improving performance.

## v3.1.0
Added count tokens method to all services.

## v3.0.0
Now targets .Net 6.0 and .Net 8.0 as well.

## v2.1.0
Added stream generate content method to all services.

## v2.0.0
Use custom HttpClient instance with services and configuration object.

## v1.0.4
- Read API key from Environment variables. 
- Refactored services and models. 
- Added Text method to just read the string "text" from the API response.

## v1.0.3
- Added configuration paramater to content generation method. It will be used to configure model and apply safety setting while generating content. 
- Removed the unused Newtonsoft.Json package from the project.

## v1.0.2
Added chat service to use Gemini Content API using text input to build freeform conversations across multiple turns.

## v1.0.1
Added vision service to use Gemini Content API using Text-and-image input.

## v1.0.0
Added a service to use Gemini Content API using Text-only input.