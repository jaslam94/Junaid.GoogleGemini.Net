using System.Reflection;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

/// <summary>
/// The lifecycle notes on the model constants. These notes carry dates that came from Google's
/// deprecations page on 2026-11-15, and one of them was wrong for a month: the old TTS models were
/// recorded as shutting down on 2026-11-17, and Google's page says 2026-12-18. The tests read the
/// attributes by reflection, so they do not trigger the obsolete warnings that they check.
/// </summary>
public class ModelLifecycleTests
{
    private static string? ObsoleteMessage(string constantName) =>
        typeof(GeminiConstants.Models).GetField(constantName, BindingFlags.Public | BindingFlags.Static)!
            .GetCustomAttribute<ObsoleteAttribute>()?.Message;

    [Theory]
    [InlineData("Gemini25FlashTts")]
    [InlineData("Gemini25ProTts")]
    [InlineData("Gemini31FlashTts")]
    public void TheOldTtsModels_ShowTheCurrentShutdownDate(string constant)
    {
        var message = ObsoleteMessage(constant);

        Assert.NotNull(message);
        Assert.Contains("2026-12-18", message);
        Assert.DoesNotContain("2026-11-17", message);
    }

    [Theory]
    [InlineData("Gemini25Pro", "gemini-3.8-flash")]
    [InlineData("Gemini25Flash", "gemini-3.5-flash-lite")]
    public void The25Models_AreObsolete_AndNameTheRoutingDateAndTarget(string constant, string routedTo)
    {
        var message = ObsoleteMessage(constant);

        Assert.NotNull(message);
        Assert.Contains("2026-12-01", message);
        Assert.Contains(routedTo, message);
    }

    [Theory]
    [InlineData("Gemini15Pro")]
    [InlineData("Gemini15Flash")]
    [InlineData("GeminiPro")]
    [InlineData("Gemini10Pro")]
    [InlineData("Embedding001")]
    [InlineData("TextEmbedding004")]
    public void TheRetiredModels_AreObsolete(string constant)
    {
        Assert.False(string.IsNullOrWhiteSpace(ObsoleteMessage(constant)));
    }

    [Theory]
    [InlineData("Gemini25Pro", "gemini-2.5-pro")]
    [InlineData("Gemini25Flash", "gemini-2.5-flash")]
    [InlineData("Gemini15Pro", "gemini-1.5-pro")]
    [InlineData("Gemini15Flash", "gemini-1.5-flash")]
    [InlineData("GeminiPro", "gemini-pro")]
    [InlineData("Gemini10Pro", "gemini-1.0-pro")]
    [InlineData("Embedding001", "embedding-001")]
    [InlineData("TextEmbedding004", "text-embedding-004")]
    [InlineData("Gemini25FlashTts", "gemini-2.5-flash-preview-tts")]
    [InlineData("Gemini25ProTts", "gemini-2.5-pro-preview-tts")]
    [InlineData("Gemini31FlashTts", "gemini-3.1-flash-tts-preview")]
    public void TheModelIds_AreUnchanged(string constant, string expected)
    {
        // Marking a constant obsolete must not change its value. Callers who still pass it need the
        // same string on the wire. Google routes or rejects it, which is Google's call.
        var value = typeof(GeminiConstants.Models).GetField(constant, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue();

        Assert.Equal(expected, value);
    }

    [Fact]
    public void EmbeddingModels_DoesNotListTheModelsThatGoogleShutDown()
    {
        Assert.DoesNotContain("embedding-001", GeminiConstants.Models.EmbeddingModels);
        Assert.DoesNotContain("text-embedding-004", GeminiConstants.Models.EmbeddingModels);
        Assert.Contains("gemini-embedding-2", GeminiConstants.Models.EmbeddingModels);
        Assert.Contains("gemini-embedding-001", GeminiConstants.Models.EmbeddingModels);
    }

    [Fact]
    public void ContentGenerationModels_DoesNotListTheModelsThatGoogleRoutesAway()
    {
        Assert.DoesNotContain("gemini-2.5-pro", GeminiConstants.Models.ContentGenerationModels);
        Assert.DoesNotContain("gemini-2.5-flash", GeminiConstants.Models.ContentGenerationModels);
        Assert.Contains("gemini-3.8-flash", GeminiConstants.Models.ContentGenerationModels);
        Assert.Contains("gemini-3.5-flash-lite", GeminiConstants.Models.ContentGenerationModels);
    }
}
