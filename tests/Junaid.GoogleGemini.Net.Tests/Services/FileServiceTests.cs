using System.Net;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

public class FileServiceTests
{
    [Fact]
    public async Task UploadFileAsync_PerformsResumableHandshake_AndReturnsFile()
    {
        // Step 1 response: empty 200 carrying the upload-session URL header.
        var start = new HttpResponseMessage(HttpStatusCode.OK);
        start.Headers.TryAddWithoutValidation("X-Goog-Upload-URL", "https://example.test/upload/session-1");

        // Step 2 response: the finalized file resource.
        const string fileJson =
            """{"file":{"name":"files/abc","mimeType":"image/png","state":"ACTIVE","uri":"https://example.test/files/abc"}}""";
        var finalize = FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, fileJson);

        var handler = FakeHttpMessageHandler.Sequence(start, finalize);
        var files = BuildFileService(handler);

        var result = await files.UploadFileAsync(new byte[] { 1, 2, 3 }, "image/png", "test.png");

        Assert.Equal("files/abc", result.Name);
        Assert.Equal("ACTIVE", result.State);
        Assert.Equal("https://example.test/files/abc", result.Uri);
        Assert.Equal(2, handler.CallCount); // start + finalize

        // The start request must declare the resumable protocol.
        Assert.True(handler.Requests[0].Headers.Contains("X-Goog-Upload-Protocol"));
    }

    [Fact]
    public async Task DownloadFileAsync_ReturnsRawBytes()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("download/v1beta/files/abc:download", req.RequestUri!.ToString());
            Assert.Contains("alt=media", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        });
        var files = BuildFileService(handler);

        var result = await files.DownloadFileAsync("files/abc");

        Assert.Equal(bytes, result);
    }

    [Fact]
    public async Task DownloadFileAsync_NormalizesBareName()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Contains("download/v1beta/files/abc:download", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 9 }) };
        });
        var files = BuildFileService(handler);

        await files.DownloadFileAsync("abc"); // no "files/" prefix supplied
    }

    private static IFileService BuildFileService(FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345";
            o.BaseUrl = new Uri("https://example.test/v1beta/");
            o.RateLimit.Enabled = false;
        });

        // Override the Files client's primary handler with our fake.
        services.AddHttpClient(GeminiHttpClients.Files).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider().GetRequiredService<IFileService>();
    }
}
