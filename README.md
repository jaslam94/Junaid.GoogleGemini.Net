# Junaid.GoogleGemini.Net

An open-source .NET library to use [Gemini API](https://ai.google.dev/tutorials/rest_quickstart) based on Google’s largest and most capable AI model yet.

## Installation of [Nuget Package](https://www.nuget.org/packages/Junaid.GoogleGemini.Net)

.NET CLI:

```shell
> dotnet add package Junaid.GoogleGemini.Net
```

Package Manager:

```shell
PM > Install-Package Junaid.GoogleGemini.Net
```

## Authentication

Get an API key from Google's AI Studio [here](https://makersuite.google.com/app/apikey). 

Either of the following three ways can be used to set the API key: 

1. Use the `GeminiConfiguration.ApiKey` property to set the secret API key directly in your application code.
    
    ```csharp
    GeminiConfiguration.ApiKey = "xxxxxxxxxxxxxxxxx";
    ``` 

2. Or pass the API key as an environment variable named "GeminiApiKey".

3. Or pass the API key as the "GeminiApiKey" field inside an `App.config` file.
    
    ```csharp
    <?xml version="1.0" encoding="utf-8" ?>
    <configuration>
	    <appSettings>
		    <add key="GeminiApiKey" value="xxxxxxxxxxxxxxxxx" />
	    </appSettings>
    </configuration>
    ```

## Services

There are five services:
1. TextService
2. VisionService
3. ChatService
4. ModelInfoService
5. EmbeddingService

A service instance needs to be created first and then its methods should be called. There are two ways of initializing a service instance. Either create an instance with the default constructor or pass in a custom `GeminiClient` object to the parameterized constructor. For information on `GeminiClient` and its usage navigate to the [GeminiClient section](#geminiclient) of this page.

The first three services from the above list contain the `GenereateContentAsync` method to generate text-only content, the `StreamGenereateContentAsync` method to provide a stream of text-only output and the `CountTokensAsync` method to count tokens.

- `GenereateContentAsync` is used to generate content in textual form. The input parameters to this method vary from service to service, however, an optional input parameter named `configuration` of type `GenerateContentConfiguration` is common among all services. For information on its usage navigate to the [configuration section](#configuration) of this page.

    The `GenereateContentAsync` method returns the `GenerateContentResponse` object. To just get the text string inside this object, use the method `Text()` as shown in the code snippets given below.

- The `StreamGenereateContentAsync` takes the same parameters as `GenereateContentAsync` in their respective service, with an additional delegate `Action<string>`.

- The `CountTokensAsync` method takes the same parameters as `GenereateContentAsync` in their respective service. It does not take the optional `configuration` parameter.

The following sections show example code snippets that highlight how to use these services.

### 1. TextService

`TextService` is used to generate content with text-only input. It has three methods.

1. The `GenereateContentAsync` method takes a mandatory `string` (text prompt) as input, an optional `GenerateContentConfiguration` (model parameters and safety settings) argument and returns the `GenerateContentResponse` response object.

    ```csharp
    var service = new TextService();
    var result = await service.GenereateContentAsync("Say Hi to me!");
    Console.WriteLine(result.Text());
    ```

2. The `StreamGenereateContentAsync` method is used to generate the stream of text-only content.

    ```csharp
    var service = new TextService();
    Action<string> handleStreamData = (data) =>
    {
        Console.WriteLine(data);
    };
    await service.StreamGenereateContentAsync("Write a story on Google AI.", handleStreamData);
    ```

3. The `CountTokensAsync` method is used to get the total tokens count. When using long prompts, it might be useful to count tokens before sending any content to the model. 

    ```csharp
    var service = new TextService();
    var result = await service.CountTokensAsync("Write a story on Google AI.");
    Console.WriteLine(result.totalTokens);
    ```

### 2. VisionService

`VisionService` is used to generate content with both text and image inputs. It has three methods.

1. The `GenereateContentAsync` method takes mandatory `string` (text prompt) and `FileObject` (file bytes and file name), an optional `GenerateContentConfiguration` (model parameters and safety settings) argument and returns the `GenerateContentResponse` response object.

    ```csharp
    string filePath = "path/<imageName.imageExtension>";
    var fileName = Path.GetFileName(filePath);
    byte[] fileBytes = Array.Empty<byte>();
    try
    {
        using (var imageStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var memoryStream = new MemoryStream())
        {
            imageStream.CopyTo(memoryStream);
            fileBytes = memoryStream.ToArray();
        }
        Console.WriteLine($"Image loaded successfully. Byte array length: {fileBytes.Length}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    var service = new VisionService();
    var result = await service.GenereateContentAsync("Explain this image?", new FileObject(fileBytes, fileName));
    Console.WriteLine(result.Text());
    ```

2. The `StreamGenereateContentAsync` method is used to generate the stream of text-only content.

    ```csharp
    ......
    var service = new VisionService();
    Action<string> handleStreamData = (data) =>
    {
        Console.WriteLine(data);
    };
    await service.StreamGenereateContentAsync("Explain this image?", new FileObject(fileBytes, fileName), handleStreamData);
    ```

3. The `CountTokensAsync` method is used to get the total tokens count. When using long prompts, it might be useful to count tokens before sending any content to the model. 

    ```csharp
    ......
    var service = new VisionService();
    var result = await service.CountTokensAsync("Explain this image?", new FileObject(fileBytes, fileName));
    Console.WriteLine(result.totalTokens);
    ```

### 3. ChatService

`ChatService` is used to generate freeform conversations across multiple turns with chat history as input. It has three methods.

1. The `GenereateContentAsync` method takes an array of `MessageObject` as an argument, an optional `GenerateContentConfiguration` (model parameters and safety settings) argument and returns the `GenerateContentResponse` response object.

    Each `MessageObject` contains two fields i.e. a `string` named role (value can be either of "model" or "user" only) and another `string` named text (text prompt).

    ```csharp
    var chat = new MessageObject[]
    {
        new MessageObject( "user", "Write the first line of a story about a magic backpack." ),
        new MessageObject( "model", "In the bustling city of Meadow brook, lived a young girl named Sophie. She was a bright and curious soul with an imaginative mind." ),
        new MessageObject( "user", "Write one more line." ),
    };

    var service = new ChatService();
    var result = await service.GenereateContentAsync(chat);
    Console.WriteLine(result.Text());
    ```

2. The `StreamGenereateContentAsync` method is used to generate the stream of text-only content.

    ```csharp
    ......
    var service = new ChatService();
    Action<string> handleStreamData = (data) =>
    {
        Console.WriteLine(data);
    };
    await service.StreamGenereateContentAsync(chat, handleStreamData);
    ```

3. The `CountTokensAsync` method is used to get the total tokens count. When using long prompts, it might be useful to count tokens before sending any content to the model. 

    ```csharp
    ......
    var service = new ChatService();
    var result = await service.CountTokensAsync(chat);
    Console.WriteLine(result.totalTokens);
    ```

#### Configuration

Configuration input can be used to control the content generation by configuring [model parameters](https://ai.google.dev/docs/concepts#model_parameters) and by using [safety settings](https://ai.google.dev/docs/safety_setting_gemini).

An example of setting the `configuration` parameter of type `GenerateContentConfiguration` and passing it to the `GenereateContentAsync` method of `TextService` is as follows:

```csharp
var configuration = new GenerateContentConfiguration
{
    safetySettings = new []
    {
        new SafetySetting
        {
            category = CategoryConstants.DangerousContent,
            threshold = ThresholdConstants.BlockOnlyHigh
        }
    },
    generationConfig = new GenerationConfig
    {
        stopSequences = new List<string> { "Title" },
        temperature = 1.0,
        maxOutputTokens = 800,
        topP = 0.8,
        topK = 10
    }
};

var service = new TextService();
var result = await service.GenereateContentAsync("Write a quote by Aristotle.", configuration);
Console.WriteLine(result.Text());
```
##

### 4. ModelInfoService

`ModelInfoService` is used to return information about the model being used to generate content. It has two methods.

1. The `ListModelsAsync` method lists all of the models available through the API, including both the Gemini and PaLM family models.

    ```csharp
    var service = new ModelInfoService();
    var result = await service.ListModelsAsync();
    ```

2. The `GetModelAsync` takes `string` (model name) as input and returns information about that model such as version, display name, input token limit, etc.

    ```csharp
    var service = new ModelInfoService();
    var result = await modelInfoService.GetModelAsync("gemini-pro-vision");
    ```

### 5. EmbeddingService

`EmbeddingService` is used to represent information as a list of floating point numbers in an array. It has two methods.

1. `EmbedContentAsync` takes a `string` (model name) and another `string` (text prompt) as arguments. It returns the `EmbedContentResponse` object.

    ```csharp
    var service = new EmbeddingService();
    var result = await service.EmbedContentAsync("embedding-001", "Write a story about a magic backpack.");
    ```
2.  `BatchEmbedContentAsync` takes a `string` (model name) and a `string[]` (array of text prompts) as arguments. It returns the `BatchEmbedContentResponse` object.

    ```csharp
    var service = new EmbeddingService();
    var result = await service.BatchEmbedContentAsync("embedding-001", new[] { "Write a story about a magic backpack.", "Say Hi to me!" });
    ```
##

### GeminiClient

`GeminiClient` contains the `ApiKey` and `HttpClient` objects. The default instance of `GeminiClient` is automatically created with the initialization of the service object. However, a case may arise where a custom `GeminiClient` is needed.

For example: **Using proxy**

In such a scenario, a custom `HttpClient` object will be used to set proxy parameters. This object will then be used to initialize the `GeminiClient`.

```csharp
using HttpClientHandler httpClientHandler = new HttpClientHandler()
{
    Proxy = new WebProxy()
    {
            Address = new Uri("xxxxxxxxxxxx"),
    },
    UseProxy = true,
};

using HttpClient httpClient = new HttpClient(httpClientHandler, false);
httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
httpClient.DefaultRequestHeaders.Add("X-Goog-Api-Key", "xxxxxxxxxxxx");

GeminiConfiguration.GeminiClient = new GeminiClient(httpClient);
```

In the above example, the `GeminiClient` instance is assigned to the static `GeminiClient` property of the `GeminiConfiguration` object. This can then be used with all instances of the different services. 

```csharp
......
GeminiConfiguration.GeminiClient = new GeminiClient(httpClient);
```

The `GeminiClient` instance can also be set at the service level. With this different instances can be used with different services.

```csharp
......
var textService = new TextService(new GeminiClient(httpClient));
var textServiceResult = await textService.GenereateContentAsync("Write a short poem on friendship.");
```
<hr />

Thanks for using this library.

- Contributions are welcome. Please read the [contributing guidelines](https://github.com/jaslam94/Junaid.GoogleGemini.Net/blob/master/Junaid.GoogleGemini.Net/CONTRIBUTING.md).

- The API is being manually released on Nuget.org. The [release notes file](https://github.com/jaslam94/Junaid.GoogleGemini.Net/blob/master/Junaid.GoogleGemini.Net/RELEASE.md) lists down the release notes.

- Feel free to contact me via [email](mailto:aslam.junaid786@hotmail.com) if you have any questions or suggestions.