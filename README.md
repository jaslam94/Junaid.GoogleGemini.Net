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

## Usage

### Authentication

Get an API key from Google's AI Studio [here](https://makersuite.google.com/app/apikey). 

There are three ways of setting the API key. Use `GeminiConfiguration.ApiKey` property to set the secret API key directly in your application code.

```csharp
GeminiConfiguration.ApiKey = "xxxxxxxxxxxxxxxxx";
``` 

You can pass the API key as an envrionment variable named "GeminiApiKey" as well.

If you are using an App.config file, you can pass the API key as "GeminiApiKey" field as well.

### TextService

`TextService` is used to generate text-only content. The `GenereateContentAsync` method takes a mandatory `string` (text prompt) as an argument and returns the textual data. 

An optional argument named `configuration` of `GenerateContentConfiguration` type can also be passed to the above method `GenereateContentAsync`. For information on its usage navigate to [configuration section](#configuration) of this page.

```csharp
var service = new TextService();
var result = await service.GenereateContentAsync("Say Hi to me!");
Console.WriteLine(result.Text());
```

The `GenereateContentAsync` method returns `GenerateContentResponse` object. To just get the text string inside this object, use the method `Text()` as shown in the code snippet above.

### VisionService

`VisionService` is used to generate content with both text and image inputs. The `GenereateContentAsync` method takes a `string` (text prompt) and `FileObject` (file bytes and file name) as an argument and returns the textual data. 

An optional argument named `configuration` of `GenerateContentConfiguration` type can also be passed to the above method `GenereateContentAsync`. For information on its usage navigate to [configuration section](#configuration) of this page.

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

The `GenereateContentAsync` method returns `GenerateContentResponse` object. To just get the text string inside this object, use the method `Text()` as shown in the code snippet above.

### ChatService

`ChatService` is used to generate freeform conversations across multiple turns with chat history as input. The `GenereateContentAsync` method takes an array of `MessageObject` as an argument. 

Each `MessageObject` contains two fields i.e. a `string` named role (value can be either of "model" or "user" only) and another `string` named text (text prompt).

An optional argument named `configuration` of `GenerateContentConfiguration` type can also be passed to the above method `GenereateContentAsync`. For information on its usage navigate to [configuration section](#configuration) of this page.

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

The `GenereateContentAsync` method returns `GenerateContentResponse` object. To just get the text string inside this object, use the method `Text()` as shown in the code snippet above.

### Configuration

Configuration input can be used to control the content generation by configuring [model parameters](https://ai.google.dev/docs/concepts#model_parameters) and by using [safety settings](https://ai.google.dev/docs/safety_setting_gemini).

An example of setting `configuration` parameter of type `GenerateContentConfiguration` and passing it to the `GenereateContentAsync` method of `TextService` is as follows:

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
result.Text();
```

 The usage of the `configuration` parameter is similar in both the `ChatService` and `VisionService`.

## Contributing

Feel free to improve the library by adding new functionality, removing outdated functionality, updating broken functionality and refactoring code by using better Software Engineering practices, styles and patterns.

### Getting Started

1. Start by forking the repository.
2. Clone the forked repository.
3. Create a new branch for your contribution.

### Contribution Guidelines

- Adhere to the established code style within the project.
- Use meaningful commit messages.
- Please test your code and document the changes before creating a pull request.
- Push your changes to your fork and initiate a pull request against the `master` branch.
- Ensure your changes do not break existing functionality.
- While creating issues include detailed information, steps to reproduce the issue and check for existing issues to avoid duplicates.

## 

Feel free to open an issue or contact me via [email](mailto:aslam.junaid786@hotmail.com) if you have any questions or suggestions.