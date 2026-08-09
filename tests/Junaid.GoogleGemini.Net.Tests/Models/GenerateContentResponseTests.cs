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

    [Fact]
    public void Images_WhenInlineImageDataPresent_DecodesIt()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic number
        var response = new GenerateContentResponse
        {
            Candidates =
            [
                new Candidate
                {
                    Content = new Content
                    {
                        Role = "model",
                        Parts =
                        [
                            new Part { Text = "Here you go:" },
                            new Part { InlineData = new InlineData { MimeType = "image/png", Data = Convert.ToBase64String(pngBytes) } }
                        ]
                    },
                    FinishReason = "STOP"
                }
            ]
        };

        var images = response.Images();
        Assert.Single(images);
        Assert.Equal("image/png", images[0].MimeType);
        Assert.Equal(pngBytes, images[0].Data);

        Assert.True(response.TryGetImages(out var tryImages));
        Assert.Single(tryImages!);

        Assert.Single(response.GetImagesOrThrow());
    }

    [Fact]
    public void Images_WhenNoInlineImageData_ReturnsEmptyAndAccessorsReportNoImages()
    {
        var response = new GenerateContentResponse
        {
            Candidates =
            [
                new Candidate
                {
                    Content = new Content { Role = "model", Parts = [new Part { Text = "No image here." }] },
                    FinishReason = "STOP"
                }
            ]
        };

        Assert.Empty(response.Images());
        Assert.False(response.TryGetImages(out var images));
        Assert.Null(images);
        Assert.Throws<GeminiContentException>(() => response.GetImagesOrThrow());
    }

    [Fact]
    public void Images_IgnoresNonImageInlineData()
    {
        // inlineData could in principle carry other binary kinds (e.g. audio) in a future response;
        // Images() must not surface those.
        var response = new GenerateContentResponse
        {
            Candidates =
            [
                new Candidate
                {
                    Content = new Content
                    {
                        Role = "model",
                        Parts = [new Part { InlineData = new InlineData { MimeType = "audio/mpeg", Data = "AAAA" } }]
                    }
                }
            ]
        };

        Assert.Empty(response.Images());
    }
}
