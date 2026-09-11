# Multimodal embeddings

`gemini-embedding-2` maps text, images, audio, video, and PDFs into one shared vector space. You
already use `IEmbeddingService` for text embeddings; the same interface now takes media too.

```csharp
var embedding = await embeddings.EmbedContentAsync(
    "gemini-embedding-2", imageBytes, "image/jpeg", text: "A red circle");
```

`text` is optional. Media alone produces a valid embedding:

```csharp
var embedding = await embeddings.EmbedContentAsync("gemini-embedding-2", audioBytes, "audio/wav");
```

Confirmed live: combining text and media does not just accept both and ignore one. A text-only
embedding and a text+image embedding of the same prompt come back with a cosine similarity around
0.5, meaningfully different but still related, real fusion, not a no-op.

## Large files

For media too large to send inline, upload it first through the Files API, then pass the URI:

```csharp
var file = await files.UploadFileAsync(videoBytes, "video/mp4", "clip.mp4");
var embedding = await embeddings.EmbedFileAsync("gemini-embedding-2", file.Uri!, "video/mp4");
```

## Batch, mixing text and media

`BatchEmbedContentAsync` has an overload taking `EmbeddingInput`, so a batch can mix plain-text and
multimodal entries. One embedding comes back per input, in the same order:

```csharp
var results = await embeddings.BatchEmbedContentAsync("gemini-embedding-2", new List<EmbeddingInput>
{
    new() { Text = "a plain text document" },
    new() { Text = "A red circle", MediaBytes = imageBytes, MimeType = "image/jpeg" },
});
```

## `TaskType` does nothing on `gemini-embedding-2`

> **This is the one thing in this article worth reading even if you skip the rest.**

`EmbeddingOptions.TaskType` (`RETRIEVAL_QUERY`, `RETRIEVAL_DOCUMENT`, and so on) works normally on
older embedding models. On `gemini-embedding-2` it does nothing: confirmed live, an identical request
sent with and without `TaskType` set returned byte-for-byte identical embeddings. The API does not
reject the field; it just silently ignores it. Google's guidance for this model is to put the task
instruction directly in the prompt text instead:

```csharp
// Instead of TaskType, write the instruction into the prompt:
var embedding = await embeddings.EmbedContentAsync(
    "gemini-embedding-2", "task: search result | query: What shipped in .NET 9?");
```

Setting `TaskType` with `gemini-embedding-2` logs a one-time warning (via the standard
`ILogger<EmbeddingService>`) rather than failing silently forever.

## Supported input, per Google's docs

Not independently verified against these exact boundaries; this library does not enforce them
client-side, since the API is the authoritative source on limits it can change without a library
update. As of this writing: up to 8,192 text tokens, 6 images (PNG/JPEG), 120 seconds of video
(MP4/MOV), 180 seconds of audio (MP3/WAV), 1 PDF up to 6 pages. Live-verified in this library's own
test suite: image, audio, PDF, and file-based input. Video input specifically was not live-tested in
this library's suite (a sandboxing limitation during development, not a doubt about the mechanism,
which is identical to the confirmed image/audio path); it should work the same way.

## Output dimensionality

```csharp
var embedding = await embeddings.EmbedContentAsync(
    "gemini-embedding-2", "some text", options: new EmbeddingOptions { OutputDimensionality = 768 });
```

Confirmed live: requesting `768` returns exactly `768` values (default is `3072`). Google's docs
recommend `768`, `1536`, or `3072`; other sizes between 128 and 3072 are also accepted.
