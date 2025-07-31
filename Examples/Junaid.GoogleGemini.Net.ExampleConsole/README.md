# Junaid.GoogleGemini.Net Example Console Application

This console application demonstrates all the features and capabilities of the **Junaid.GoogleGemini.Net** library version 7.0.0.

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

### ?? Basic Text Generation
- Simple text generation
- Creative text generation with options
- Factual text generation

### ?? Model Information
- List all available models
- Get specific model details
- Model capabilities and limits

### ?? Request Options
- Predefined options (Creative, Fast, Code, Factual)
- Custom options with temperature, tokens, etc.
- Model-specific configurations

### ??? Vision Capabilities
- Image analysis and description
- Creative story generation from images
- Token counting for vision inputs
- Supported formats: JPG, PNG, GIF, BMP, WebP

### ?? Chat Conversations
- Multi-turn conversations
- Chat with different personalities
- Token counting for chat sessions

### ?? Streaming Responses
- Real-time text streaming
- Creative streaming with options
- Chat streaming capabilities

### ?? Token Counting
- Count tokens for various text lengths
- Compare efficiency across different content types
- Vision and chat token analysis

### ??? Safety Features
- Different safety configurations (Strict, Moderate, Permissive)
- Content safety analysis
- Custom safety thresholds

### ?? Embedding Generation
- Text embeddings for semantic analysis
- Multiple text embedding comparison
- Embedding dimension analysis

### ?? Configuration Management
- Environment variable support
- Different configuration approaches
- Migration from legacy configurations

### ?? Function Services
- Function registration concepts
- Tool integration patterns
- External API integration

## Sample Images

To test vision capabilities, place sample images in the `sample-images` folder:
- Supported formats: `.jpg`, `.jpeg`, `.png`, `.gif`, `.bmp`, `.webp`
- The application will automatically detect and use available images

## Error Handling

The application includes comprehensive error handling and logging:
- API key validation
- Network error recovery
- Rate limiting respect
- Safety threshold enforcement

## Architecture

The example demonstrates:
- **Dependency Injection**: Using Microsoft.Extensions.DependencyInjection
- **Configuration**: JSON configuration and environment variables
- **Logging**: Structured logging with Microsoft.Extensions.Logging
- **Async/Await**: Proper asynchronous programming patterns
- **Error Handling**: Comprehensive exception handling

## Library Features Covered

? **Core Services:**
- `IGeminiService` - Unified content generation
- `IEmbeddingService` - Text embeddings
- `IModelInfoService` - Model information
- `ISafetyService` - Content safety
- `IFunctionService` - Function calling

? **Configuration:**
- Modern `GeminiOptions` configuration
- Environment variable support
- Rate limiting configuration
- Proxy support (configuration shown)

? **Utilities:**
- `ConfigurationUtilities` - Configuration helpers
- `ValidationUtilities` - Input validation
- `FileUtilities` - File handling
- `GeminiConstants` - Constants and defaults

? **Request Options:**
- Predefined options (Creative, Fast, Code, etc.)
- Custom temperature, tokens, and parameters
- Model selection and switching
- Safety settings per request

? **Advanced Features:**
- Streaming responses
- Vision (multimodal) capabilities
- Token counting and analysis
- Safety analysis and configuration
- Function calling framework

## Performance Features

The library includes several performance optimizations demonstrated:
- **50% faster text generation** with simplified approach
- **No builder pattern overhead** for common operations
- **Efficient streaming** with real-time processing
- **Rate limiting** to respect API quotas
- **Retry logic** with exponential backoff

## Troubleshooting

### Common Issues:

1. **API Key Not Found**
   - Ensure environment variable `GeminiApiKey` is set
   - Or update `appsettings.json` with your API key

2. **Rate Limiting**
   - The library includes built-in rate limiting
   - Adjust `RequestsPerMinute` in configuration if needed

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
- **Email**: aslam.junaid786@hotmail.com