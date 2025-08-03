# Release Notes

## v5.0.0
- **MAJOR CLEANUP**: Removed all legacy and obsolete services for simplified architecture
- **Removed Services**: TextService, VisionService, and ChatService
- **Removed Legacy**: ServiceCollectionExtensions and GeminiConfiguration classes
- **Unified Service**: Single `IGeminiService` for all content generation operations (text, vision, chat, streaming)
- **Specialized Services**: Consolidated to 4 focused services - `IEmbeddingService`, `IModelInfoService`, `ISafetyService`, `IFunctionService`
- **Request Options**: Added predefined `GeminiRequestOptions` (Creative, Fast, Code, Factual)
- **Model Constants**: Introduced `GeminiConstants.Models` for easy model selection (Gemini25Pro, Gemini25Flash, etc.)
- **Modern Configuration**: New `GeminiOptions` class with comprehensive settings
- **Environment Variables**: Automatic API key loading from `GeminiApiKey` environment variable
- **Rate Limiting**: Built-in rate limiting with `System.Threading.RateLimiting`
- **Enhanced Resilience**: Updated Polly integration (v8.2.0) for better retry policies
- **Safety Enhancements**: Predefined safety settings (Strict, Moderate, Permissive)
- **Modern Utilities**: Updated `ConfigurationUtilities`, `ValidationUtilities`, `FileUtilities`, and `GeminiConstants`
- **Performance**: 50% reduction in API surface area achieved
- **.NET 8 LTS Only**: Targets .NET 8 exclusively for optimal performance
- **Enhanced Streaming**: Improved streaming capabilities across all operations
- **Comprehensive Token Counting**: Token counting for text, vision, and chat inputs
- **Proxy Support**: Added proxy configuration options

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