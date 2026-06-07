using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Models;

/// <summary>
/// Tests the response text accessors: Text() returns the text (or empty string, never a placeholder
/// sentence), TryGetText distinguishes "no text", and GetTextOrThrow surfaces a typed error.
/// </summary>
public class GenerateContentResponseTests
{
    [Fact]
    public void Text_WhenCandidatePresent_ReturnsText()
    {
        var response = new GenerateContentResponse
        {
            Candidates =
            [
                new Candidate
                {
                    Content = new Content { Role = "model", Parts = [new Part { Text = "Hi there" }] },
                    FinishReason = "STOP"
                }
            ]
        };

        Assert.Equal("Hi there", response.Text());
        Assert.True(response.TryGetText(out var text));
        Assert.Equal("Hi there", text);
        Assert.Equal("STOP", response.FinishReason);
    }

    [Fact]
    public void Text_WhenNoCandidates_ReturnsEmptyAndAccessorsReportNoText()
    {
        var response = new GenerateContentResponse { Candidates = [] };

        Assert.Equal(string.Empty, response.Text());
        Assert.False(response.TryGetText(out var text));
        Assert.Null(text);
        Assert.Throws<GeminiContentException>(() => response.GetTextOrThrow());
    }
}
