using System.Text.Json;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

public class ModelVersionTests
{
    [Fact]
    public void ModelVersion_IsReadFromTheResponse()
    {
        // Real shape, seen on 2026-10-10: a request for gemini-3.5-flash was answered by gemini-3.6-flash.
        const string json = """{"candidates":[{"content":{"parts":[{"text":"OK"}]}}],"modelVersion":"gemini-3.6-flash"}""";

        var response = JsonSerializer.Deserialize<GenerateContentResponse>(json, GeminiJson.Default)!;

        Assert.Equal("gemini-3.6-flash", response.ModelVersion);
    }

    [Fact]
    public void ModelVersion_IsNullWhenTheApiOmitsIt()
    {
        var response = JsonSerializer.Deserialize<GenerateContentResponse>(
            """{"candidates":[{"content":{"parts":[{"text":"OK"}]}}]}""", GeminiJson.Default)!;

        Assert.Null(response.ModelVersion);
    }
}
