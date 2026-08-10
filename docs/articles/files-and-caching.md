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
await files.DeleteFileAsync(file.Name!);
```

Uploads use the resumable protocol over a dedicated HttpClient. Reference an uploaded file in a
request with a `FileData` part (`MimeType` + `FileUri`).

## Context caching

Cache a large, reused payload (long context, system instruction, tools) once, then reference it by
name from later requests to save tokens and latency. Inject `ICachingService`.

```csharp
var cache = await caching.CreateAsync(new CachedContent
{
    Model = "models/gemini-2.5-flash",
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

> **Note:** context caching requires a **billing-enabled** API key. The free tier allows zero
> cached-content storage, so `CreateAsync` fails there with a `TotalCachedContentStorageTokensPerModelFreeTier`
> error. Cached content also has a per-model **minimum token count** (for example, roughly 1,024 for Gemini 2.5 Flash).
