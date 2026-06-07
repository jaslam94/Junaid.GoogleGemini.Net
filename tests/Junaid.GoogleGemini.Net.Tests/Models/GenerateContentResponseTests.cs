using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Models;

/// <summary>
/// Characterization tests: these lock in the CURRENT behavior of <see cref="GenerateContentResponse.Text()"/>
/// so we have a safety net. Phase 1 intentionally changes this behavior (magic strings → typed result),
/// at which point these tests are updated to describe the new contract.
/// </summary>
public class GenerateContentResponseTests
{
    [Fact]
    public void Text_WhenCandidatePresent_ReturnsText()
    {
        var response = new GenerateContentResponse
        {
            candidates =
            [
                new Candidate
                {
                    content = new Content { Role = "model", Parts = [new Part { Text = "Hi there" }] },
                    finishReason = "STOP"
                }
            ]
        };

        Assert.Equal("Hi there", response.Text());
    }

    [Fact]
    public void Text_WhenNoCandidates_ReturnsPlaceholder()
    {
        var response = new GenerateContentResponse { candidates = [] };

        Assert.Equal("[No content generated - response contained no candidates]", response.Text());
    }
}
