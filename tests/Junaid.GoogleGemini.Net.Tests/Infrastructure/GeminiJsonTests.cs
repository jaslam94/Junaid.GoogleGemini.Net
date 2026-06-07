using System.Text.Json;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// Locks in the unified wire contract: camelCase JSON, null members omitted, and correct
/// round-tripping through the shared <see cref="GeminiJson.Default"/> options (source-gen + fallback).
/// </summary>
public class GeminiJsonTests
{
    [Fact]
    public void Serialize_Request_UsesCamelCaseAndOmitsNulls()
    {
        var request = new GenerateContentRequest
        {
            Contents = [new Content { Role = "user", Parts = [new Part { Text = "hi" }] }],
            GenerationConfig = new GenerationConfig { Temperature = 0.5f }
        };

        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"contents\"", json);
        Assert.Contains("\"role\":\"user\"", json);
        Assert.Contains("\"temperature\":0.5", json);
        // Null members must not be written to the wire.
        Assert.DoesNotContain("safetySettings", json);
        Assert.DoesNotContain("topK", json);
    }

    [Fact]
    public void Deserialize_Response_MapsCamelCaseWireToPascalCaseModel()
    {
        const string json = """
        {"candidates":[{"content":{"role":"model","parts":[{"text":"ok"}]},"finishReason":"STOP"}],
         "usageMetadata":{"promptTokenCount":3,"candidatesTokenCount":1,"totalTokenCount":4}}
        """;

        var response = JsonSerializer.Deserialize<GenerateContentResponse>(json, GeminiJson.Default)!;

        Assert.Equal("ok", response.Text());
        Assert.Equal("STOP", response.Candidates![0].FinishReason);
        Assert.Equal(4, response.UsageMetadata!.TotalTokenCount);
    }
}
