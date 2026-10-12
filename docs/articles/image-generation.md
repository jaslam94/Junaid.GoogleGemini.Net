# Image generation

Gemini's "Nano Banana" models can return generated images from the same `generateContent` endpoint
used for text; the client just needs to ask for `IMAGE` output. `GenerateImageAsync` does that for you.

> **Note:** `Gemini31FlashImage` (Nano Banana 2) and `Gemini3ProImage` (Nano Banana Pro) are both
> GA since May 2026, under their current unsuffixed model IDs. Earlier releases of this library
> pointed these constants at the older `-preview` model IDs. Google shut those down on June 25,
> 2026. If you pinned the string literal instead of the constant, upgrade it. The library also has
> `Gemini31FlashLiteImage` (Nano Banana 2 Lite), the fastest and cheapest option.

```csharp
var response = await gemini.GenerateImageAsync("A watercolor painting of a lighthouse at sunset.");

foreach (var image in response.Images())
    await File.WriteAllBytesAsync($"lighthouse.{image.MimeType.Split('/')[1]}", image.Data);
```

## Reading the result

Mirrors the `Text()` accessor trio on `GenerateContentResponse`:

```csharp
IReadOnlyList<GeneratedImage> images = response.Images();           // empty list if none, never throws
bool found = response.TryGetImages(out var maybeImages);            // false + null when there are none
IReadOnlyList<GeneratedImage> guaranteed = response.GetImagesOrThrow(); // throws GeminiContentException if none
```

Each `GeneratedImage` is just `MimeType` (for example `"image/png"`) and already-decoded `Data`
(`byte[]`), so no base64 handling is required.

## Choosing a model and quality

`GenerateImageAsync` defaults `Model` to `GeminiConstants.Models.RecommendedImage` when you don't set
one. That is still `gemini-3.1-flash-image`. Google deprecated it on October 6, 2026 and has not set a
shutdown date.

Google's replacement is Nano Banana 2.1 (`gemini-nano-banana-2.1`, GA October 6, 2026). Pass it
explicitly with `Model = GeminiConstants.Models.GeminiNanoBanana21`. It charges half the
image-output rate of the old model ($30 against $60 per 1M tokens) but three times the input rate
($1.50 against $0.50). It has no free tier.

We did not make it the default because of what we saw on October 10, 2026. Nano Banana 2.1 answered
simple prompts with `finishReason: IMAGE_RECITATION` and no image. "A small red circle on a plain
white background" failed 2 of 2 calls. A plain blue rectangle failed 1 of 6 calls, and the same
prompt gave an image on the next call. The old model returned an image for the red circle. If you
use Nano Banana 2.1, check `response.FinishReason` when `Images()` is empty, and retry. We will
change the default when Google sets a shutdown date for the old model.

For higher quality, pass the pro model explicitly:

```csharp
var options = new GeminiRequestOptions { Model = GeminiConstants.Models.Gemini3ProImage };
var response = await gemini.GenerateImageAsync("A hyper-detailed matte painting of a lighthouse.", options);
```

## Aspect ratio and resolution

Gemini 3+ image models accept an aspect ratio and target resolution:

```csharp
var options = new GeminiRequestOptions
{
    Model = GeminiConstants.Models.Gemini3ProImage,
    ImageAspectRatio = GeminiConstants.ImageAspectRatios.Widescreen16x9,
    ImageSize = GeminiConstants.ImageSizes.TwoK,
};
```

`ImageAspectRatios` covers the 14 ratios in Google's API reference (`1:1`, `3:2`, `2:3`, `3:4`, `4:3`,
`4:5`, `5:4`, `9:16`, `16:9`, `21:9`, and the strips `1:4`, `4:1`, `1:8`, `8:1`). `ImageSizes` covers
`512`, `1K`, `2K` and `4K`. Both properties are optional, so you can omit them and let the model choose.

The strip ratios came with Nano Banana 2.1. We checked `1:8` against the real API and got a
352×2928 image. The `512` size is not available on Nano Banana 2.1. The API returns HTTP 400
("Image size 512 is not supported for this model"). Google documents it for
`gemini-3.1-flash-image`.

## Response modalities (full control)

`GenerateImageAsync` sets `ResponseModalities = [TEXT, IMAGE]` by default, which works on both older
and current image models. Some current models also accept image-only output. Set it yourself for
full control, or to use image generation via the plain `GenerateAsync`/`ChatAsync` calls instead:

```csharp
var options = new GeminiRequestOptions
{
    Model = GeminiConstants.Models.Gemini31FlashImage,
    ResponseModalities = [GeminiConstants.ResponseModalities.Image],
};
```

> **Note:** image generation requires a billing-enabled API key. The free tier has a quota of 0
> for `generate_content` on image models (`generate_content_free_tier_requests`, limit: 0). On a
> free-tier key, calls fail with a `GeminiApiException` ("You exceeded your current quota..."). This
> release has no streaming for image generation. A future release may add it if Google exposes
> partial-image streaming more broadly.
