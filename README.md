# Junaid.GoogleGemini.Net

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)
![C#](https://img.shields.io/badge/C%23-12.0-blue.svg)
![NuGet](https://img.shields.io/nuget/v/Junaid.GoogleGemini.Net.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)
![Build](https://img.shields.io/badge/build-passing-brightgreen.svg)

An open-source .NET library to use [Gemini API](https://ai.google.dev/tutorials/rest_quickstart) based on Google's largest and most capable AI model yet.

## Installation

### NuGet Package

.NET CLI:
```shell
dotnet add package Junaid.GoogleGemini.Net
```

Package Manager:
```shell
Install-Package Junaid.GoogleGemini.Net
```

## Authentication

Get an API key from Google's AI Studio [here](https://makersuite.google.com/app/apikey).

### Option 1: Environment Variable (Recommended)
```bash
# Set environment variable
export GeminiApiKey="your-api-key-here"

# Or on Windows
set GeminiApiKey=your-api-key-here
```

### Option 2: Configuration File
```json
{
  "Gemini": {
    "ApiKey": "your-api-key-here",
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "DefaultModel": "gemini-2.5-pro",
    "RateLimit": {
      "Enabled": true,
      "RequestsPerMinute": 60,
      "TokensPerMinute": 60000
    }
  }
}
```

## Quick Start

Register the services and start using:

```csharp
// Register services
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));

// Use in your application
app.MapGet("/", async (IGeminiService gemini) =>
{
    var response = await gemini.GenerateAsync("Say hello to me!");
    return response.Text();
});
```

## Core Features

### Unified IGeminiService
Single service for all content generation operations:

```csharp
// Text generation
var response = await gemini.GenerateAsync("Write a story about AI");

// Vision (text + image)
var imageBytes = File.ReadAllBytes("image.jpg");
var image = new FileObject(imageBytes, "image.jpg");
var response = await gemini.GenerateWithImageAsync("What's in this image?", image);

// Chat conversations
var messages = new[]
{
    new MessageObject("user", "Hello, who are you?"),
    new MessageObject("model", "I'm Gemini, an AI assistant.")
};
var response = await gemini.ChatAsync(messages);

// Streaming
await gemini.StreamAsync("Tell me a long story", chunk => Console.Write(chunk));
```

### Request Options
Control generation behavior with predefined or custom options:

```csharp
// Predefined options
var creative = await gemini.GenerateAsync("Write a poem", GeminiRequestOptions.Creative());
var factual = await gemini.GenerateAsync("Explain physics", GeminiRequestOptions.Factual());
var code = await gemini.GenerateAsync("Write a function", GeminiRequestOptions.Code());
var fast = await gemini.GenerateAsync("Quick answer", GeminiRequestOptions.Fast());

// Custom options
var custom = new GeminiRequestOptions
{
    Temperature = 0.7f,
    MaxTokens = 1000,
    Model = GeminiConstants.Models.Gemini25Pro
};
var response = await gemini.GenerateAsync("Your prompt", custom);
```

### Model Selection
Choose the right model for your use case:

```csharp
// Latest and most capable model
GeminiConstants.Models.Gemini25Pro     // "gemini-2.5-pro"

// Fastest model for quick responses  
GeminiConstants.Models.Gemini25Flash   // "gemini-2.5-flash"

// Alternative models
GeminiConstants.Models.Gemini20Flash   // "gemini-2.0-flash-001"
GeminiConstants.Models.Gemini15Pro     // "gemini-1.5-pro"
GeminiConstants.Models.Gemini15Flash   // "gemini-1.5-flash"

// Recommended and fastest shortcuts
GeminiConstants.Models.Recommended     // Points to Gemini25Pro
GeminiConstants.Models.Fastest         // Points to Gemini25Flash
```

### Token Counting
Monitor and optimize your API usage:

```csharp
// Count tokens for text
var tokens = await gemini.CountTokensAsync("Your text here");
Console.WriteLine($"Token count: {tokens.totalTokens}");

// Count tokens for vision
var visionTokens = await gemini.CountTokensWithImageAsync("Describe image", image);

// Count tokens for chat
var chatTokens = await gemini.CountTokensChatAsync(messages);
```

## Specialized Services

### Embedding Service
Generate vector embeddings for semantic analysis:

```csharp
// Single embedding
var embedding = await embeddingService.EmbedContentAsync("text-embedding-004", "Your text");

// Batch embeddings
var embeddings = await embeddingService.BatchEmbedContentAsync("text-embedding-004", texts);
```

### Model Info Service
Get information about available models:

```csharp
var models = await modelService.ListModelsAsync();
var modelInfo = await modelService.GetModelAsync("gemini-2.5-pro");
```

### Safety Service
Configure content safety and analyze responses:

```csharp
var strictSafety = safetyService.CreateStrictSafetySettings();
var moderateSafety = safetyService.CreateModerateSafetySettings();
var permissiveSafety = safetyService.CreatePermissiveSafetySettings();

var isContentSafe = safetyService.IsContentSafe(response, thresholds);
```

### Function Service
Register and call custom functions:

```csharp
functionService.RegisterFunction(weatherFunction, weatherHandler);
var result = await functionService.CallFunctionAsync(functionCall);
```

## Configuration

### Modern Configuration
```csharp
builder.Services.AddGemini(options =>
{
    options.ApiKey = "your-api-key-here"; // or set GeminiApiKey environment variable
    options.TimeoutSeconds = 30;
    options.MaxRetries = 3;
    options.DefaultModel = GeminiConstants.Models.Recommended;
    
    // Configure rate limiting
    options.RateLimit = new RateLimitOptions
    {
        Enabled = true,
        RequestsPerMinute = 60,
        TokensPerMinute = 60000
    };
});
```

### Environment Variables
API key is automatically loaded from `GeminiApiKey` environment variable:

```csharp
// API key will be automatically loaded from environment
builder.Services.AddGemini(options =>
{
    options.DefaultModel = GeminiConstants.Models.Recommended;
});
```

## Examples

Explore comprehensive examples in our [Console Application](https://github.com/jaslam94/Junaid.GoogleGemini.Net/tree/master/Examples/Junaid.GoogleGemini.Net.ExampleConsole) demonstrating:

- Text generation with various options
- Vision capabilities with image analysis  
- Chat conversations and streaming
- Token counting and optimization
- Safety settings and content analysis
- Embedding generation
- Function calling
- Advanced integration patterns

### Creative Writing
```csharp
var story = await gemini.GenerateAsync(
    "Write a short science fiction story about time travel",
    GeminiRequestOptions.Creative());
```

### Code Generation
```csharp
var code = await gemini.GenerateAsync(
    "Create a C# function that reverses a string",
    GeminiRequestOptions.Code());
```

### Data Analysis
```csharp
var analysis = await gemini.GenerateWithImageAsync(
    "Analyze this chart and explain the trends",
    chartImage,
    GeminiRequestOptions.Factual());
```

## What's New in v5.0.0

- **Major Cleanup**: Removed all legacy services for simplified architecture
- **Performance**: 50% reduction in API surface area
- **Unified Service**: Single `IGeminiService` for all content generation
- **Modern Utilities**: Updated configuration and utility system
- **.NET 8 Optimized**: Enhanced performance with latest .NET features

## Requirements

- **.NET 8.0** or later
- **Google AI Studio API Key** - Get yours [here](https://makersuite.google.com/app/apikey)

## Contributing

Contributions are welcome! Please read our [contributing guidelines](https://github.com/jaslam94/Junaid.GoogleGemini.Net/blob/master/Junaid.GoogleGemini.Net/CONTRIBUTING.md).

## License

This project is licensed under the MIT License.

## Support

- **GitHub**: [Issues and Discussions](https://github.com/jaslam94/Junaid.GoogleGemini.Net)
- **NuGet**: [Package](https://www.nuget.org/packages/Junaid.GoogleGemini.Net)
- **Release**: [Notes](https://github.com/jaslam94/Junaid.GoogleGemini.Net/blob/master/Junaid.GoogleGemini.Net/RELEASE.md)

Thanks for using Junaid.GoogleGemini.Net. Feel free to [email me](mailto:aslam.junaid786@hotmail.com) if you have any questions or suggestions.