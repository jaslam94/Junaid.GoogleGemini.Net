# Junaid.GoogleGemini.Net

An open-source .NET library to use [Gemini API](https://ai.google.dev/tutorials/rest_quickstart) based on Google’s largest and most capable AI model yet. 

## Installation

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

Get an API key from Google's AI Studio [here](https://makersuite.google.com/app/apikey). Use `GeminiConfiguration.ApiKey` property to set the secret key.

`GeminiConfiguration.ApiKey = "xxxxxxxxxxxxxxxxx";`

### TextService

`TextService` is used to generate text-only content. The `GenereateContentAsync` method takes a `string` (text prompt) as an argument and returns the textual data.

```csharp
var service = new TextService();
var result = await service.GenereateContentAsync("Say Hi to me!");
```

### VisionService

`VisionService` is used to generate content with both text and image inputs. The `GenereateContentAsync` method takes a `string` (text prompt) and `FileObject` (file bytes and file name) as an argument and returns the textual data.

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
var result = await service.GenereateContentAsync("Explain this image?", new Junaid.GoogleGemini.Net.Models.Requests.FileObject(fileBytes, fileName));
```

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

Feel free to open an issue or contact me if you have any questions or suggestions.