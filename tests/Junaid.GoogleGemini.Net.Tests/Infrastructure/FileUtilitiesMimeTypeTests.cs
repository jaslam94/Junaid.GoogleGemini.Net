using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// <c>FileUtilities.GetMimeType</c>. The Office, OpenDocument, audio and video strings come from
/// Google's document, audio and video pages (read on 2026-11-18). Before 6.9.0 the method returned
/// <c>application/octet-stream</c> for every Office type.
/// </summary>
public class FileUtilitiesMimeTypeTests
{
    [Theory]
    // Office and OpenDocument
    [InlineData("report.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("report.doc", "application/msword")]
    [InlineData("report.dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template")]
    [InlineData("report.odt", "application/vnd.oasis.opendocument.text")]
    [InlineData("slides.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("slides.ppt", "application/vnd.ms-powerpoint")]
    [InlineData("slides.odp", "application/vnd.oasis.opendocument.presentation")]
    [InlineData("sheet.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("sheet.xls", "application/vnd.ms-excel")]
    [InlineData("sheet.ods", "application/vnd.oasis.opendocument.spreadsheet")]
    // Audio
    [InlineData("clip.aiff", "audio/aiff")]
    [InlineData("clip.ogg", "audio/ogg")]
    // Video
    [InlineData("clip.mpeg", "video/mpeg")]
    [InlineData("clip.mpg", "video/mpg")]
    [InlineData("clip.flv", "video/x-flv")]
    [InlineData("clip.wmv", "video/wmv")]
    [InlineData("clip.3gp", "video/3gpp")]
    // Pinned. Google lists "video/avi", but nobody has tested which string it accepts for a real
    // file, so ".avi" keeps its value. ".mp3" is pinned the same way. A change needs a live test.
    [InlineData("clip.avi", "video/x-msvideo")]
    [InlineData("clip.mp3", "audio/mpeg")]
    public void GetMimeType_ReturnsTheExpectedType(string fileName, string expected)
    {
        Assert.Equal(expected, FileUtilities.GetMimeType(fileName));
    }

    [Theory]
    [InlineData("REPORT.DOCX")]
    [InlineData(@"C:\Users\someone\Documents\Report.Docx")]
    [InlineData("/home/someone/report.docx")]
    public void GetMimeType_IgnoresCaseAndFolders(string fileName)
    {
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileUtilities.GetMimeType(fileName));
    }

    [Fact]
    public void GetMimeType_StillFallsBackForAnUnknownExtension()
    {
        Assert.Equal("application/octet-stream", FileUtilities.GetMimeType("archive.zip"));
        Assert.Equal("application/octet-stream", FileUtilities.GetMimeType("noextension"));
    }

    [Theory]
    [InlineData("report.docx")]
    [InlineData("sheet.xlsx")]
    [InlineData("slides.pptx")]
    [InlineData("clip.mpg")]
    [InlineData("clip.aiff")]
    public void TheNewTypes_AreNotImages(string fileName)
    {
        // The image checks must not change. Image requests still reject these files.
        Assert.False(FileUtilities.IsImageFile(fileName));
    }

    [Fact]
    public void GetSupportedImageExtensions_ListsOnlyImageExtensions()
    {
        var extensions = FileUtilities.GetSupportedImageExtensions();

        Assert.Contains(".png", extensions);
        Assert.Contains(".jpg", extensions);
        Assert.DoesNotContain(".docx", extensions);
        Assert.DoesNotContain(".mpg", extensions);
        Assert.DoesNotContain(".ogg", extensions);
    }
}
