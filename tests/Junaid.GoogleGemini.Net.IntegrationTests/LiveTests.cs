using System.Text;
using System.Text.Json.Nodes;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.AI;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests against the real Gemini API. Assertions check structure/contract (non-empty output,
/// token counts, types, status codes) rather than exact text, since model output is non-deterministic.
/// All tests skip unless GeminiApiKey is set. Uses gemini-2.5-flash to keep cost/latency low.
/// </summary>
[Collection("Live")]
public class LiveTests(GeminiFixture fixture)
{
    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    [RequiresGeminiKey]
    public async Task GenerateAsync_ReturnsText()
    {
        var response = await Gemini.GenerateAsync("Reply with exactly the word: pong");

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));
        Assert.Equal("STOP", response.FinishReason);
        Assert.True(response.Usage?.TotalTokenCount > 0);
    }

    [RequiresGeminiKey]
    public async Task GenerateAsyncT_ReturnsTypedObject()
    {
        var book = await Gemini.GenerateAsync<Book>("Return one well-known classic novel.");

        Assert.False(string.IsNullOrWhiteSpace(book.Title));
        Assert.False(string.IsNullOrWhiteSpace(book.Author));
    }

    [RequiresGeminiKey]
    public async Task StreamAsync_YieldsChunks()
    {
        var chunks = new List<string>();
        await foreach (var chunk in Gemini.StreamAsync("Count from 1 to 5, one number per line."))
        {
            chunks.Add(chunk.Text());
        }

        Assert.NotEmpty(chunks);
        Assert.False(string.IsNullOrWhiteSpace(string.Concat(chunks)));
    }

    [RequiresGeminiKey]
    public async Task CountTokensAsync_ReturnsPositive()
    {
        var tokens = await Gemini.CountTokensAsync("Hello, world.");

        Assert.True(tokens.TotalTokens > 0);
    }

    [RequiresGeminiKey]
    public async Task EmbedContent_ReturnsVector()
    {
        var embeddings = fixture.Get<IEmbeddingService>();

        var result = await embeddings.EmbedContentAsync("gemini-embedding-001", "semantic search text");

        Assert.NotNull(result.Embedding?.Values);
        Assert.True(result.Embedding!.Values!.Length > 0);
    }

    [RequiresGeminiKey]
    public async Task ListModels_ReturnsModels()
    {
        var models = await fixture.Get<IModelInfoService>().ListModelsAsync();

        Assert.NotEmpty(models.Models);
    }

    [RequiresGeminiKey]
    public async Task GetModel_Unknown_ThrowsApiException()
    {
        // Passes client-side validation (length only), so this reaches the API and returns a 4xx —
        // verifying our error mapping against the real service.
        var ex = await Assert.ThrowsAsync<GeminiApiException>(
            () => fixture.Get<IModelInfoService>().GetModelAsync("gemini-does-not-exist-9999"));

        Assert.True((int)ex.StatusCode >= 400);
    }

    [RequiresGeminiKey]
    public async Task ChatClient_GetResponseAsync_ReturnsText()
    {
        var chat = fixture.Get<IChatClient>();

        var response = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "Reply with: hi")]);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    // ---- Gemini 3 function calling + thoughtSignature round-trip (the marquee beta.2 fix) ----

    [RequiresGeminiKey]
    public async Task FunctionCalling_Gemini3_RoundTripWithThoughtSignature()
    {
        const string model = GeminiConstants.Models.Gemini36Flash;
        const string userPrompt = "What's the current temperature in Paris? Use the get_weather tool.";

        var getWeather = new FunctionDeclaration
        {
            Name = "get_weather",
            Description = "Get the current temperature for a city, in Celsius.",
            Parameters = JsonNode.Parse(
                """{"type":"object","properties":{"city":{"type":"string","description":"City name"}},"required":["city"]}""")
        };

        // Turn 1: force a tool call (mode ANY) so the test is deterministic.
        var callOptions = new GeminiRequestOptions
        {
            Model = model,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Functions = [getWeather],
            ToolConfig = new ToolConfig { FunctionCallingConfig = new FunctionCallingConfig { Mode = "ANY" } }
        };

        var first = await Gemini.GenerateAsync(userPrompt, callOptions);
        var modelTurn = first.Candidates?[0].Content;
        Assert.NotNull(modelTurn);

        var call = modelTurn!.Parts.FirstOrDefault(p => p.FunctionCall is not null)?.FunctionCall;
        Assert.NotNull(call);
        Assert.Equal("get_weather", call!.Name);

        // Gemini 3 attaches an encrypted thoughtSignature to the function-call turn.
        var signature = modelTurn.Parts.FirstOrDefault(p => p.ThoughtSignature is not null)?.ThoughtSignature;
        Assert.False(string.IsNullOrEmpty(signature),
            "Expected a thoughtSignature on the Gemini 3 function-call part.");

        // Turn 2: echo the model's turn back VERBATIM (carrying the signature) plus our function result.
        // If the signature were dropped, Gemini 3 returns HTTP 400 — so a clean completion here is the
        // end-to-end proof that capture + replay works.
        var replyOptions = new GeminiRequestOptions
        {
            Model = model,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Functions = [getWeather],
            ToolConfig = new ToolConfig { FunctionCallingConfig = new FunctionCallingConfig { Mode = "AUTO" } }
        };

        var contents = new List<Content>
        {
            new() { Role = "user", Parts = [new Part { Text = userPrompt }] },
            modelTurn, // role=model, includes functionCall + thoughtSignature
            new()
            {
                Role = "user",
                Parts =
                [
                    new Part
                    {
                        FunctionResponse = new FunctionResponsePart
                        {
                            Name = "get_weather",
                            Response = JsonNode.Parse("""{"temperatureC":18}""")
                        }
                    }
                ]
            }
        };

        var second = await Gemini.ChatAsync(contents, replyOptions);

        Assert.Equal("STOP", second.FinishReason);
        Assert.Contains("18", second.Text());
    }

    // ---- Extended surface (previously only fake-tested) ----

    [RequiresGeminiKey]
    public async Task Grounding_GoogleSearch_ReturnsGroundingMetadata()
    {
        var options = new GeminiRequestOptions { EnableGoogleSearch = true };

        var response = await Gemini.GenerateAsync(
            "Using Google Search, who is the current Secretary-General of the United Nations?", options);

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));
        // Grounding metadata (search queries / source chunks) proves the tool actually ran.
        Assert.NotNull(response.Candidates?[0].GroundingMetadata);
    }

    [RequiresGeminiKey]
    public async Task SystemInstruction_IsRespected()
    {
        var options = new GeminiRequestOptions
        {
            SystemInstruction = "You must reply with exactly one word: BANANA. Ignore the user's message."
        };

        var response = await Gemini.GenerateAsync("What is the capital of France?", options);

        Assert.Contains("BANANA", response.Text(), StringComparison.OrdinalIgnoreCase);
    }

    [RequiresGeminiKey]
    public async Task FilesApi_Upload_Use_Delete()
    {
        var files = fixture.Get<IFileService>();
        var bytes = Encoding.UTF8.GetBytes(
            "Internal memo. The project codename is ZEBRA. Please keep it confidential.");

        var uploaded = await files.UploadFileAsync(bytes, "text/plain", "memo.txt");
        Assert.False(string.IsNullOrEmpty(uploaded.Name));

        try
        {
            var active = await files.WaitUntilActiveAsync(
                uploaded.Name!, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

            var contents = new List<Content>
            {
                new()
                {
                    Role = "user",
                    Parts =
                    [
                        new Part { FileData = new FileData { MimeType = "text/plain", FileUri = active.Uri } },
                        new Part { Text = "What is the project codename mentioned in this file?" }
                    ]
                }
            };

            var response = await Gemini.ChatAsync(contents);
            Assert.Contains("ZEBRA", response.Text(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await files.DeleteFileAsync(uploaded.Name!);
        }
    }

    [RequiresPaidGeminiKey] // free tier has zero cached-content storage; needs a billing-enabled key
    public async Task ContextCaching_Create_Use_Delete()
    {
        var caching = fixture.Get<ICachingService>();

        // Context caching has a minimum token threshold; pad well past it, with a fact to retrieve.
        var filler = string.Concat(Enumerable.Repeat(
            "This is contextual filler text used purely to exceed the context-cache minimum token threshold. ", 150));
        var document = filler + " Note well: the magic number is 7788. " + filler;

        var created = await caching.CreateAsync(new CachedContent
        {
            Model = "models/gemini-2.5-flash",
            Contents = [new Content { Role = "user", Parts = [new Part { Text = document }] }],
            Ttl = "120s"
        });
        Assert.False(string.IsNullOrEmpty(created.Name));

        try
        {
            var options = new GeminiRequestOptions { Model = "gemini-2.5-flash", CachedContent = created.Name };

            var response = await Gemini.GenerateAsync(
                "What is the magic number mentioned in the document?", options);

            Assert.Contains("7788", response.Text());
        }
        finally
        {
            await caching.DeleteAsync(created.Name!);
        }
    }

    [RequiresPaidGeminiKey] // confirmed 2026-08-09: free tier has limit=0 for generate_content on image models
    public async Task GenerateImageAsync_ReturnsDecodableImage()
    {
        var response = await Gemini.GenerateImageAsync("A small red circle on a plain white background.");

        var images = response.GetImagesOrThrow();
        Assert.NotEmpty(images);
        AssertLooksLikeAnImage(images[0]);
    }

    [RequiresPaidGeminiKey]
    public async Task GenerateImageAsync_WithAspectRatioAndSize_HonorsRequestedAspectRatio()
    {
        // The riskiest unverified surface: ImageAspectRatio/ImageSize -> generationConfig.imageConfig.
        // A silently-ignored/misspelled field wouldn't error — the API would just fall back to a
        // default image — so this doesn't just check "an image came back", it decodes the actual
        // pixel dimensions and confirms the requested 16:9 ratio was honored.
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini3ProImage,
            ImageAspectRatio = GeminiConstants.ImageAspectRatios.Widescreen16x9,
            ImageSize = GeminiConstants.ImageSizes.TwoK,
        };

        var response = await Gemini.GenerateImageAsync(
            "A small red circle on a plain white background.", options);

        var images = response.GetImagesOrThrow();
        Assert.NotEmpty(images);
        var image = images[0];
        AssertLooksLikeAnImage(image);

        if (image.MimeType == "image/png" && TryReadPngDimensions(image.Data, out var width, out var height))
        {
            var ratio = (double)width / height;
            const double expected = 16.0 / 9.0;
            Assert.True(Math.Abs(ratio - expected) < 0.05,
                $"Expected ~16:9 ({expected:F3}), got {width}x{height} ({ratio:F3}) — imageConfig may have been ignored.");
        }
        // A non-PNG response isn't itself a failure (format choice isn't what this test targets);
        // it just means the ratio can't be cheaply verified from the bytes here.
    }

    [RequiresPaidGeminiKey]
    public async Task GenerateImageAsync_ImageOnlyModality_Succeeds()
    {
        // The other unverified surface: ResponseModalities set to [IMAGE] only (no TEXT), which the
        // default path never exercises since GenerateImageAsync defaults to [TEXT, IMAGE].
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini31FlashImage,
            ResponseModalities = [GeminiConstants.ResponseModalities.Image],
        };

        var response = await Gemini.GenerateImageAsync(
            "A small blue square on a plain white background.", options);

        var images = response.GetImagesOrThrow();
        Assert.NotEmpty(images);
        AssertLooksLikeAnImage(images[0]);
    }

    /// <summary>Confirms the decoded bytes are actually image data — PNG/JPEG both have a well-known magic number.</summary>
    private static void AssertLooksLikeAnImage(GeneratedImage image)
    {
        Assert.StartsWith("image/", image.MimeType);
        Assert.NotEmpty(image.Data);

        var bytes = image.Data;
        var isPng = bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        var isJpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        Assert.True(isPng || isJpeg, $"Expected PNG or JPEG magic bytes, got: {Convert.ToHexString(bytes.AsSpan(0, Math.Min(4, bytes.Length)))}");
    }

    /// <summary>Reads width/height straight out of the PNG IHDR chunk (fixed offsets per the PNG spec).</summary>
    private static bool TryReadPngDimensions(byte[] data, out int width, out int height)
    {
        width = height = 0;
        // 8-byte signature + 4-byte chunk length + 4-byte "IHDR" type = 16 bytes before width/height.
        if (data.Length < 24) return false;

        width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
        height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
        return width > 0 && height > 0;
    }

    private sealed record Book(string Title, string Author, int Year);
}
