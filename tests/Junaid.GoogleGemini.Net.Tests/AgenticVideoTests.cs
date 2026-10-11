using System.Text.Json;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

/// <summary>
/// Agentic video (<c>Part.MediaProcessing = "AGENTIC"</c>). The response shape and numbers below come from
/// a real call to gemini-3.8-flash on 2026-10-10 with a 660-token video clip.
/// </summary>
public class AgenticVideoTests
{
    // Trimmed from the real response. The answer arrives after toolCall and toolResponse parts, and the
    // video tokens are reported in toolUsePromptTokenCount, not in promptTokenCount.
    private const string AgenticResponse = """
        {
          "candidates": [{
            "content": {"role": "model", "parts": [
              {"toolCall": {"id": "call_811817"}},
              {"toolResponse": {"id": "call_811817"}},
              {"text": "A waiter scoops vanilla ice cream into a small white bowl."}
            ]},
            "finishReason": "STOP"
          }],
          "usageMetadata": {
            "promptTokenCount": 11,
            "candidatesTokenCount": 63,
            "totalTokenCount": 786,
            "promptTokensDetails": [{"modality": "TEXT", "tokenCount": 11}],
            "toolUsePromptTokenCount": 712,
            "toolUsePromptTokensDetails": [
              {"modality": "TEXT", "tokenCount": 52},
              {"modality": "VIDEO", "tokenCount": 660}
            ]
          }
        }
        """;

    private static GeminiCostGovernor Governor(Dictionary<string, ModelPricing>? overrides = null, DateTime? now = null) =>
        new(new BudgetOptions { Enabled = true, ModelPricingOverrides = overrides },
            (_, _) => { },
            utcNowProvider: () => now ?? new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void AgenticResponse_ParsesToolUseTokens_AndStillReturnsTheText()
    {
        var response = JsonSerializer.Deserialize<GenerateContentResponse>(AgenticResponse, GeminiJson.Default)!;

        Assert.Equal("A waiter scoops vanilla ice cream into a small white bowl.", response.Text());
        Assert.Equal(11, response.Usage!.PromptTokenCount);
        Assert.Equal(712, response.Usage.ToolUsePromptTokenCount);
        Assert.Equal(786, response.Usage.TotalTokenCount); // 11 + 63 + 712: the total already includes tool use
        Assert.Contains(response.Usage.ToolUsePromptTokensDetails!, d => d.Modality == "VIDEO" && d.TokenCount == 660);
    }

    [Fact]
    public void AResponseWithoutToolUse_HasZeroToolUseTokens()
    {
        const string plain = """{"candidates":[{"content":{"parts":[{"text":"hi"}]}}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":1,"totalTokenCount":6}}""";

        var response = JsonSerializer.Deserialize<GenerateContentResponse>(plain, GeminiJson.Default)!;

        Assert.Equal(0, response.Usage!.ToolUsePromptTokenCount);
        Assert.Null(response.Usage.ToolUsePromptTokensDetails);
    }

    [Fact]
    public void CostGovernor_PricesToolUseTokensAsInput()
    {
        // gemini-3.8-flash at its introductory rate: $0.75 in, $3.75 out per 1M tokens.
        //   input  = (11 + 712) / 1M * 0.75 = 0.00054225
        //   output = 63 / 1M * 3.75        = 0.00023625
        var usage = JsonSerializer.Deserialize<GenerateContentResponse>(AgenticResponse, GeminiJson.Default)!.Usage!;

        var cost = Governor().RecordSpend("gemini-3.8-flash", usage);

        Assert.Equal(0.0007785m, cost);
    }

    [Fact]
    public void CostGovernor_BeforeThisFix_WouldHaveCalledTheVideoFree()
    {
        // The same call with the tool-use tokens removed. This is what 6.7.0 computed. It is about 3 times
        // too low for this call.
        var withoutToolUse = new UsageMetadata { PromptTokenCount = 11, CandidatesTokenCount = 63 };
        var withToolUse = new UsageMetadata { PromptTokenCount = 11, CandidatesTokenCount = 63, ToolUsePromptTokenCount = 712 };

        var low = Governor().RecordSpend("gemini-3.8-flash", withoutToolUse);
        var right = Governor().RecordSpend("gemini-3.8-flash", withToolUse);

        Assert.Equal(0.0002445m, low);
        Assert.True(right > low * 3);
    }

    [Fact]
    public void ToolUseTokens_CountTowardTheHighVolumeTier()
    {
        // 60 prompt tokens alone stay under the 100-token threshold. With 60 tool-use tokens, the call is
        // over it and the higher rate applies.
        var pricing = new ModelPricing
        {
            InputPerMillionTokensUsd = 1m,
            OutputPerMillionTokensUsd = 1m,
            CachedInputPerMillionTokensUsd = 1m,
            HighVolumeTier = new HighVolumeTier
            {
                ThresholdTokens = 100,
                InputPerMillionTokensUsd = 10m,
                OutputPerMillionTokensUsd = 10m,
                CachedInputPerMillionTokensUsd = 10m,
            },
        };
        var governor = Governor(new Dictionary<string, ModelPricing> { ["m"] = pricing });

        var under = governor.RecordSpend("m", new UsageMetadata { PromptTokenCount = 60 });
        var over = governor.RecordSpend("m", new UsageMetadata { PromptTokenCount = 60, ToolUsePromptTokenCount = 60 });

        Assert.Equal(60m / 1_000_000m * 1m, under);
        Assert.Equal(120m / 1_000_000m * 10m, over);
    }

    [Fact]
    public void CachedTokens_AreNotDoubleCounted_WhenToolUseIsPresent()
    {
        var pricing = new ModelPricing
        {
            InputPerMillionTokensUsd = 1m,
            OutputPerMillionTokensUsd = 0m,
            CachedInputPerMillionTokensUsd = 0.1m,
        };
        var governor = Governor(new Dictionary<string, ModelPricing> { ["m"] = pricing });

        var cost = governor.RecordSpend("m", new UsageMetadata
        {
            PromptTokenCount = 1_000_000,
            CachedContentTokenCount = 400_000,
            ToolUsePromptTokenCount = 100_000,
        });

        // (1M - 400k) at $1 + 100k tool use at $1 + 400k cached at $0.10 = 0.60 + 0.10 + 0.04
        Assert.Equal(0.74m, cost);
    }
}
