# Junaid.GoogleGemini.Net Example Console Application

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)
![C#](https://img.shields.io/badge/C%23-12.0-blue.svg)
![Google Gemini](https://img.shields.io/badge/Google-Gemini%20API-orange.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Version](https://img.shields.io/badge/Library-v5.0.0-red.svg)

This console application demonstrates all the features and capabilities of the **Junaid.GoogleGemini.Net** library v5.0.0.

## Prerequisites

1. **API Key**: Get your Google AI Studio API key from [here](https://makersuite.google.com/app/apikey)
2. **.NET 8**: Make sure you have .NET 8 SDK installed

## Configuration

### Option 1: Environment Variable (Recommended)
Set the `GeminiApiKey` environment variable:

**Windows:**
```cmd
set GeminiApiKey=your-actual-api-key-here
```

**Linux/macOS:**
```bash
export GeminiApiKey=your-actual-api-key-here
```

### Option 2: Configuration File
Update the `appsettings.json` file:
```json
{
  "Gemini": {
    "ApiKey": "your-actual-api-key-here"
  }
}
```

## Running the Application

1. Navigate to the console application directory:
```bash
cd Junaid.GoogleGemini.Net.ExampleConsole
```

2. Run the application:
```bash
dotnet run
```

## Features Demonstrated

### Basic Text Generation
- Simple text generation
- Creative text generation with options
- Factual text generation

### Model Information
- List all available models
- Get specific model details
- Model capabilities and limits

### Request Options
- Predefined options (Creative, Fast, Code, Factual)
- Custom options with temperature, tokens, etc.
- Model-specific configurations

### Vision Capabilities
- Image analysis and description
- Creative story generation from images
- Token counting for vision inputs
- Supported formats: JPG, PNG, GIF, BMP, WebP

### Chat Conversations
- Multi-turn conversations
- Chat with different personalities
- Token counting for chat sessions

### Streaming Responses
- Real-time text streaming
- Creative streaming with options
- Chat streaming capabilities

### Token Counting
- Count tokens for various text lengths
- Compare efficiency across different content types
- Vision and chat token analysis

### Safety Features
- Different safety configurations (Strict, Moderate, Permissive)
- Content safety analysis
- Custom safety thresholds

### Embedding Generation
- Text embeddings for semantic analysis
- Multiple text embedding comparison
- Embedding dimension analysis

### Configuration Management
- Environment variable support
- Modern unified configuration
- Development vs Production settings

### Function Services
- Function registration concepts
- Tool integration patterns
- External API integration

### Advanced Examples
- Performance optimization techniques
- Content generation patterns
- Error handling and recovery
- Integration patterns
- Monitoring and analytics

## Sample Images

To test vision capabilities, place sample images in the `sample-images` folder:
- Supported formats: `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.webp`
- The application will automatically detect and use available images

## Interactive Mode

The application features **interactive mode** to help manage API usage:
- Prompts before each API call
- Rate limit awareness (free tier: ~limited requests/minute)
- Cost control for paid tiers
- Exit anytime with `Q`

## Architecture

The example demonstrates:
- **Dependency Injection**: Using Microsoft.Extensions.DependencyInjection
- **Configuration**: JSON configuration and environment variables
- **Logging**: Structured logging with Microsoft.Extensions.Logging
- **Async/Await**: Proper asynchronous programming patterns
- **Error Handling**: Comprehensive exception handling

## Library Features Covered

**Core Services:**
- `IGeminiService` - Unified content generation
- `IEmbeddingService` - Text embeddings
- `IModelInfoService` - Model information
- `ISafetyService` - Content safety
- `IFunctionService` - Function calling

**Modern Configuration:**
- `GeminiOptions` configuration
- Environment variable support
- Rate limiting configuration
- Proxy support

**Unified Utilities:**
- `ConfigurationUtilities` - Configuration helpers
- `ValidationUtilities` - Input validation
- `FileUtilities` - File handling
- `GeminiConstants` - Constants and defaults

**Request Options:**
- Predefined options (Creative, Fast, Code, etc.)
- Custom temperature, tokens, and parameters
- Model selection and switching
- Safety settings per request

## Troubleshooting

### Common Issues:

1. **API Key Not Found**
   - Ensure environment variable `GeminiApiKey` is set
   - Or update `appsettings.json` with your API key

2. **Rate Limiting**
   - The library includes built-in rate limiting
   - Use interactive mode to control API calls
   - Consider upgrading to paid tier

3. **Vision Examples Skipped**
   - Place sample images in `sample-images` folder
   - Supported: JPG, PNG, GIF, BMP, WebP

4. **Network Issues**
   - Check internet connection
   - Library includes automatic retry logic
   - Configure proxy if needed in `GeminiOptions`

## Next Steps

After running this example:
1. Explore the library's source code for advanced usage
2. Integrate specific features into your applications  
3. Check the [library documentation](https://github.com/jaslam94/Junaid.GoogleGemini.Net) for updates
4. Review the [release notes](https://github.com/jaslam94/Junaid.GoogleGemini.Net/blob/master/RELEASE.md) for version changes

## Support

- **GitHub**: [Junaid.GoogleGemini.Net](https://github.com/jaslam94/Junaid.GoogleGemini.Net)
- **NuGet**: [Package](https://www.nuget.org/packages/Junaid.GoogleGemini.Net)
