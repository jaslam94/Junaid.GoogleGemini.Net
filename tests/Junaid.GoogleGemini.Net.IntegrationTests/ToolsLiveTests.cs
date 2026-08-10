using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests for the <c>code_execution</c> and <c>url_context</c> tools -- previously never
/// live-tested at all. Writing these surfaced two real gaps (fixed alongside, same session): the API's
/// <c>executableCode</c>/<c>codeExecutionResult</c> parts and <c>urlContextMetadata</c> field had no
/// corresponding model properties, so they silently vanished on deserialization even though these
/// tools "worked" in the loose sense that the model's text summary usually restated the answer.
/// </summary>
[Collection("Live")]
public class ToolsLiveTests(GeminiFixture fixture)
{
    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    [RequiresGeminiKey]
    public async Task CodeExecution_ReturnsExecutableCodeAndRealOutput()
    {
        var options = new GeminiRequestOptions { EnableCodeExecution = true };

        var response = await Gemini.GenerateAsync(
            "What is 12345 times 6789? Use the code execution tool to compute it exactly, " +
            "then state the final numeric answer.",
            options);

        var parts = response.Candidates?[0].Content?.Parts;
        Assert.NotNull(parts);

        var code = parts!.FirstOrDefault(p => p.ExecutableCode is not null)?.ExecutableCode;
        Assert.NotNull(code);
        Assert.False(string.IsNullOrWhiteSpace(code!.Code));
        Assert.Contains("12345", code.Code);

        var result = parts.FirstOrDefault(p => p.CodeExecutionResult is not null)?.CodeExecutionResult;
        Assert.NotNull(result);
        Assert.Equal("OUTCOME_OK", result!.Outcome);
        // The real, executed output -- not just the model restating the answer in prose.
        Assert.Contains("83810205", result.Output);

        // The model's text summary should also land on the correct answer.
        Assert.Contains("83,810,205", response.Text());
    }

    [RequiresGeminiKey]
    public async Task UrlContext_ReturnsUrlRetrievalMetadata()
    {
        var options = new GeminiRequestOptions { EnableUrlContext = true };

        var response = await Gemini.GenerateAsync(
            "In one sentence, summarize what https://www.anthropic.com/news is about, " +
            "using the URL context tool.",
            options);

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));

        var urlMetadata = response.Candidates?[0].UrlContextMetadata?.UrlMetadata;
        Assert.NotNull(urlMetadata);
        Assert.NotEmpty(urlMetadata!);
        Assert.Contains(urlMetadata!, m =>
            m.RetrievedUrl == "https://www.anthropic.com/news" &&
            m.UrlRetrievalStatus == "URL_RETRIEVAL_STATUS_SUCCESS");

        // The existing GroundingMetadata typed model should also carry the same source.
        var groundingSources = response.Candidates?[0].GroundingMetadata?.GroundingChunks;
        Assert.NotNull(groundingSources);
        Assert.Contains(groundingSources!, c => c.Web?.Uri == "https://www.anthropic.com/news");
    }
}
