using System.Text.Json;
using System.Text.Json.Nodes;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
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

    // Fixture trimmed from a real live GenerateImageAsync response (2026-08-15, gemini-3.1-flash-image)
    // captured while auditing this library: promptTokensDetails/candidatesTokensDetails carry a
    // per-modality token breakdown that UsageMetadata previously had no properties for, so it was
    // silently dropped on every response (image-gen or not) despite the API actually sending it.
    [Fact]
    public void Deserialize_Response_CapturesModalityTokenBreakdown()
    {
        const string json = """
        {"candidates":[{"content":{"role":"model","parts":[{"inlineData":{"mimeType":"image/jpeg","data":"//"}}]},
         "finishReason":"STOP","index":0}],
         "usageMetadata":{"promptTokenCount":9,"candidatesTokenCount":1452,"totalTokenCount":1461,
         "promptTokensDetails":[{"modality":"TEXT","tokenCount":9}],
         "candidatesTokensDetails":[{"modality":"IMAGE","tokenCount":1120}],"serviceTier":"standard"},
         "modelVersion":"gemini-3.1-flash-image","responseId":"zWGAas6iN_X6nsEPlpPRmAE"}
        """;

        var response = JsonSerializer.Deserialize<GenerateContentResponse>(json, GeminiJson.Default)!;
        var usage = response.UsageMetadata!;

        Assert.NotNull(usage.PromptTokensDetails);
        Assert.Equal("TEXT", usage.PromptTokensDetails![0].Modality);
        Assert.Equal(9, usage.PromptTokensDetails[0].TokenCount);

        Assert.NotNull(usage.CandidatesTokensDetails);
        Assert.Equal("IMAGE", usage.CandidatesTokensDetails![0].Modality);
        Assert.Equal(1120, usage.CandidatesTokensDetails[0].TokenCount);
    }

    [Fact]
    public void RequestFactory_AppliesSystemInstructionThinkingAndJsonMode()
    {
        var options = new GeminiRequestOptions
        {
            Temperature = 0.2f,
            SystemInstruction = "You are a helpful assistant.",
            ResponseMimeType = "application/json",
            ThinkingBudget = 0,
        };

        var request = RequestFactory.CreateTextRequest("hi", options);
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"systemInstruction\"", json);
        Assert.Contains("You are a helpful assistant.", json);
        Assert.Contains("\"responseMimeType\":\"application/json\"", json);
        Assert.Contains("\"thinkingConfig\"", json);
        Assert.Contains("\"thinkingBudget\":0", json);
    }

    [Fact]
    public void RequestFactory_BuildsTools_FromFlagsAndFunctions()
    {
        var options = new GeminiRequestOptions
        {
            EnableGoogleSearch = true,
            Functions =
            [
                new FunctionDeclaration
                {
                    Name = "get_weather",
                    Description = "Get the weather",
                    Parameters = JsonNode.Parse("""{"type":"object"}""")
                }
            ]
        };

        var request = RequestFactory.CreateTextRequest("weather?", options);
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"googleSearch\"", json);
        Assert.Contains("\"functionDeclarations\"", json);
        Assert.Contains("get_weather", json);
    }

    [Fact]
    public void Deserialize_Response_WithFunctionCallAndGrounding()
    {
        const string json = """
        {"candidates":[{"content":{"parts":[{"functionCall":{"name":"get_weather","args":{"city":"Paris"}}}]},
          "groundingMetadata":{"webSearchQueries":["weather paris"],
            "groundingChunks":[{"web":{"uri":"https://example.com","title":"Weather"}}]}}]}
        """;

        var response = JsonSerializer.Deserialize<GenerateContentResponse>(json, GeminiJson.Default)!;
        var part = response.Candidates![0].Content!.Parts[0];

        Assert.Equal("get_weather", part.FunctionCall!.Name);
        Assert.Equal("Paris", part.FunctionCall.Args!["city"]!.ToString());
        Assert.Equal("weather paris", response.Candidates[0].GroundingMetadata!.WebSearchQueries![0]);
        Assert.Equal("https://example.com", response.Candidates[0].GroundingMetadata!.GroundingChunks![0].Web!.Uri);
    }

    [Fact]
    public void EmbeddingRequest_IncludesTaskTypeAndDimensionality()
    {
        var request = RequestFactory.CreateEmbeddingRequest("hello", new EmbeddingOptions
        {
            TaskType = GeminiConstants.EmbeddingTaskTypes.RetrievalDocument,
            OutputDimensionality = 256,
            Title = "Doc"
        });

        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"taskType\":\"RETRIEVAL_DOCUMENT\"", json);
        Assert.Contains("\"outputDimensionality\":256", json);
        Assert.Contains("\"title\":\"Doc\"", json);
    }
}
