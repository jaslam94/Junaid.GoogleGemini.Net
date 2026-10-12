# Files & context caching

## Files API

Upload large media once and reference it by URI instead of inlining bytes. Inject `IFileService`.

```csharp
var file = await files.UploadFileAsync(bytes, "video/mp4", "clip.mp4");

// Video/audio must finish processing before use:
await files.WaitUntilActiveAsync(file.Name!);

// Other operations:
var info = await files.GetFileAsync(file.Name!);
var list = await files.ListFilesAsync(pageSize: 20);
var bytes = await files.DownloadFileAsync(file.Name!);
await files.DeleteFileAsync(file.Name!);
```

Uploads use the resumable protocol over a dedicated HttpClient. Reference an uploaded file in a
request with a `FileData` part (`MimeType` + `FileUri`). `DownloadFileAsync` fetches a file's raw
bytes back; the [Batch API](batch-api.md) uses it internally to retrieve file-based results, but it
works for any uploaded file.

## Video and media processing

Set `Part.MediaProcessing` to choose how the model reads a video part. The values are in
`GeminiConstants.MediaProcessing`. Google ignores the field on parts that are not video.

- `STATIC` extracts frames at a fixed rate and puts all of them in the context. Leaving the field
  unset behaves the same.
- `AGENTIC` lets the model navigate the video and ask for transcripts, frames, or audio as it needs
  them. Google says it uses up to 88% fewer tokens on long videos.

We tested both on 2026-10-10 against `gemini-3.8-flash` with two real clips of about 10 seconds:

- Unset and `STATIC` gave the same result (671 prompt tokens).
- `AGENTIC` answered correctly. Its response also holds `toolCall` and `toolResponse` parts, which
  the library does not model. `Text()` still returns the answer.
- `AGENTIC` reports the video as `ToolUsePromptTokenCount` and leaves `PromptTokenCount` at 11.
  Cost governance counts those tokens as input (see [Cost governance](cost-governance.md)). The
  OpenTelemetry token metrics do not (see [Observability](observability.md)).
- On these short clips, `AGENTIC` used slightly more input tokens (723 against 671). We have not
  tested the savings on long videos.

Use the Files API for a large video. A file that you upload works in agentic mode with or without a
`MimeType` on the `FileData` part. For an external URL, set `MimeType`. Without it, the API returned
HTTP 400 ("mime_type must be set when media_processing is specified"). A public YouTube URL returned
HTTP 403 on our project.

## Context caching

Cache a large, reused payload (long context, system instruction, tools) once, then reference it by
name from later requests to save tokens and latency. Inject `ICachingService`.

```csharp
var cache = await caching.CreateAsync(new CachedContent
{
    Model = "models/gemini-3.8-flash",
    Contents = [ /* large shared context */ ],
    Ttl = "3600s",
});

var answer = await gemini.GenerateAsync(
    "Summarize the document.",
    new GeminiRequestOptions { CachedContent = cache.Name });

// Manage the cache:
await caching.UpdateTtlAsync(cache.Name!, "7200s");
var all = await caching.ListAsync();
await caching.DeleteAsync(cache.Name!);
```

The response's `Usage.CachedContentTokenCount` shows how many tokens were served from the cache.

> **Note:** context caching requires a billing-enabled API key. The free tier allows zero
> cached-content storage, so `CreateAsync` fails there with a `TotalCachedContentStorageTokensPerModelFreeTier`
> error. Cached content also has a per-model minimum token count (for example, roughly 1,024 for Gemini 2.5 Flash).
