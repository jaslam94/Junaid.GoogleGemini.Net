# Image generation

Gemini's "Nano Banana" models can return generated images from the same `generateContent` endpoint
used for text — the client just needs to ask for `IMAGE` output. `GenerateImageAsync` does that for you.

> **Note:** both `Gemini31FlashImage` and `Gemini3ProImage` are still **preview** models per Google's
> own naming (the `-preview` suffix in the model ID) — not GA yet, unlike the current flagship text
> models. Expect the usual preview caveats: behavior/pricing can change, and availability may vary.

```csharp
var response = await gemini.GenerateImageAsync("A watercolor painting of a lighthouse at sunset.");

foreach (var image in response.Images())
    await File.WriteAllBytesAsync($"lighthouse.{image.MimeType.Split('/')[1]}", image.Data);
```

## Reading the result

Mirrors the `Text()` accessor trio on `GenerateContentResponse`:

```csharp
IReadOnlyList<GeneratedImage> images = response.Images();           // empty list if none — never throws
bool found = response.TryGetImages(out var maybeImages);            // false + null when there are none
IReadOnlyList<GeneratedImage> guaranteed = response.GetImagesOrThrow(); // throws GeminiContentException if none
```

Each `GeneratedImage` is just `MimeType` (e.g. `"image/png"`) and already-decoded `Data` (`byte[]`) —
no base64 handling required.

## Choosing a model and quality

`GenerateImageAsync` defaults `Model` to `GeminiConstants.Models.RecommendedImage` (the efficient flash
image model) when you don't set one — consistent with `Models.Recommended` also defaulting to the
flash text model rather than the pricier "pro" one. For higher quality, pass the pro model explicitly:

```csharp
var options = new GeminiRequestOptions { Model = GeminiConstants.Models.Gemini3ProImage };
var response = await gemini.GenerateImageAsync("A hyper-detailed matte painting of a lighthouse.", options);
```

## Aspect ratio & resolution

Gemini 3+ image models accept an aspect ratio and target resolution:

```csharp
var options = new GeminiRequestOptions
{
    Model = GeminiConstants.Models.Gemini3ProImage,
    ImageAspectRatio = GeminiConstants.ImageAspectRatios.Widescreen16x9,
    ImageSize = GeminiConstants.ImageSizes.TwoK,
};
```

`ImageAspectRatios` covers the 10 supported ratios (`1:1`, `3:2`, `2:3`, `3:4`, `4:3`, `4:5`, `5:4`,
`9:16`, `16:9`, `21:9`); `ImageSizes` covers `1K`/`2K`/`4K`. Both are optional — omit them to let the
model choose.

## Response modalities (full control)

`GenerateImageAsync` sets `ResponseModalities = [TEXT, IMAGE]` by default, which works on both older
and current image models. Some current models also accept image-only output — set it yourself for
full control, or to use image generation via the plain `GenerateAsync`/`ChatAsync` calls instead:

```csharp
var options = new GeminiRequestOptions
{
    Model = GeminiConstants.Models.Gemini31FlashImage,
    ResponseModalities = [GeminiConstants.ResponseModalities.Image],
};
```

> **Note:** image generation requires a **billing-enabled** API key — the free tier has a **0 quota**
> for `generate_content` on image models (`generate_content_free_tier_requests`, limit: 0), so calls
> fail with a `GeminiApiException` ("You exceeded your current quota...") on a free-tier key, not just
> a slow/expensive one. There's also no streaming support for image generation in this release — a
> future release may add it if Google exposes partial-image streaming more broadly.
