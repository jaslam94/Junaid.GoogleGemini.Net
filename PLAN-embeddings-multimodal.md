# Plan: Multimodal embeddings input for Junaid.GoogleGemini.Net

Status: implemented and shipped in `6.7.0`. We verified the research live against the real API on
2026-09-11, before any code. After implementation, we verified it live again in the same session,
through the actual `IEmbeddingService` code and not raw REST calls. The second pass covered image,
audio, file-based, PDF, and mixed-batch input. It also confirmed through the typed client that
`TaskType` has zero effect on `gemini-embedding-2`. The full solution build had 0 warnings on all
three targets. All 185 unit tests passed (13 are new). All 43 non-Batch live tests passed (6 are
new), with no regression to any existing feature. Video input remains unconfirmed. See §6.

## 1. Goal

`gemini-embedding-2` has accepted image, video, audio, and PDF input since GA (April 2026). It maps
all of them into one shared vector space alongside text. This library's `EmbedContentAsync` and
`BatchEmbedContentAsync` are still text-only (`string` and `string[]` parameters). A caller cannot
reach that capability today, even though the model supports it.

`ROADMAP.md` has flagged this gap since the August 2026 audit. It is now the most overdue item on the
backlog. It is smaller than TTS or STT, because it completes an existing feature and does not add a
new API surface. So we picked it as the next thing to build, with the same rigor: verify live, plan,
then code.

## 2. What we verified live, and how

We confirmed every claim below with a real REST call against `gemini-embedding-2`, using real media.
The media came from the library's own shipped features: a JPEG from image generation and a WAV from
TTS. `PLAN-stt.md` used the same approach of testing one verified feature with the output of another.

The existing wire types already support this. Only the public method signatures do not.
`EmbedContentRequest` and `SingleEmbedContentRequest` use the same `Content` and `Part` type as the
rest of this library (`inlineData`, `fileData`). It is not a text-only type. `EmbedContentAsync(model,
text, ...)` cannot send an image today because `RequestFactory.CreateEmbeddingContent(text)` always
builds a text-only `Part`. The request shape can represent more. This is a smaller change than TTS
or STT. It needs no new model types, no new endpoint, and no new client method. The gap sits
entirely in the public surface of `EmbeddingService`.

Real request shape for image input:
```json
POST https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent
{
  "content": {
    "parts": [
      {"text": "A red circle"},
      {"inlineData": {"mimeType": "image/jpeg", "data": "<base64 jpeg>"}}
    ]
  }
}
```
The call returned `HTTP 200`. The response shape did not change (`{"embedding": {"values": [3072
floats]}}`). It uses the `EmbedContentResponse` type that this library already has, so no new
response type is needed.

The image changes the embedding. We compared the
text+image embedding above with a text-only `"A red circle"` embedding. The cosine similarity was
`0.528`. That is not `1.0` and not near zero, and the vectors are not byte-identical. The model fuses
the two inputs.

Real request shape for audio input, with no text instruction:
```json
{"content": {"parts": [{"inlineData": {"mimeType": "audio/wav", "data": "<base64 wav>"}}]}}
```
The call returned `HTTP 200` and a `3072`-dimension embedding.

`fileData` (a Files API URI) works the same way as inline data. We uploaded the same JPEG through the
existing resumable-upload flow (`IFileService.UploadFileAsync`, unchanged). Then we embedded it by
URI: `{"fileData": {"mimeType": "image/jpeg", "fileUri": "https://.../files/..."}}`. The call
returned `HTTP 200` with the same shape. The Files API integration point is free here too. This
matches `PLAN-stt.md` §3.2 for STT. Large media needs no new upload code.

`batchEmbedContents` already accepts multimodal content per request. The batch endpoint needs no
change. We sent one plain-text request and one text+image request in the same batch call. We got
back 2 separate `3072`-dimension embeddings with `HTTP 200`. The existing structure of
`BatchEmbedContentAsync` (one `Content` per request) already matches the API's "separate embeddings"
semantics. Google's docs and this live call both confirm that. The only fix is to let a caller put
more than a text `Part` into each request's `Content`. This is the same fix as the single-embed case.

We found a real footgun. The API does not document it as an error. `taskType` is silently accepted
and has zero effect on `gemini-embedding-2`. We sent the identical text with and without
`"taskType": "RETRIEVAL_QUERY"`. Both calls returned `HTTP 200`, and the two response bodies were
byte-for-byte identical. Google's docs say `task_type` "cannot be used" for this model, and that
task instructions go in the prompt text instead. The API does not enforce that. It accepts the
field, does nothing with it, and returns 200 either way. Today, a caller who passes
`EmbeddingOptions.TaskType` with `gemini-embedding-2` gets no error and no effect. This is worse
for DX than a hard rejection. It needs a prominent warning, and a docs footnote is not enough.

`outputDimensionality` truncation works as documented. We requested `768` and got exactly `768`
values back (the default is `3072`). We did not check whether Google's claimed auto-normalization
renormalizes the truncated vector to unit length. We did not verify that either way this session.

We did not independently verify video and PDF input this session. Both use the same `inlineData` and
`fileData` request shape that we confirmed for two other modalities (image and audio) through the
same model and endpoint. We have high confidence, but no proof. We had no quick way to produce real
test video or PDF content in this session. TTS and image generation had given us ready-made audio
and image content. Verify both in this library's own live test suite before you consider this plan's
test coverage complete (see §7).

We did not independently verify Google's documented per-request limits (6 images max, 120s video max,
180s audio max, 1 PDF of 6 pages max). We took them from the docs and did not test the exact
boundaries. If the limits matter for a production deployment, the API's own error message on an
oversized request is the authoritative source. This library does not plan to enforce these limits
client-side. The policy is the same as everywhere else: the API rejects bad input, and we do not
pre-validate against a moving target (see §5).

## 3. Explicit scope

In scope (v1):
- `EmbedContentAsync` overloads that accept inline media bytes (plus optional text) for a single
  embedding.
- An overload that accepts a Files API URI (plus optional text) for large media. It reuses
  `IFileService` unchanged.
- A `BatchEmbedContentAsync` overload that accepts multimodal inputs per request. It mirrors the
  existing batch structure. We confirmed live that the endpoint already supports this once each
  request's `Content` can carry more than text.
- A prominent warning that `TaskType` is silently ignored on `gemini-embedding-2` (confirmed live in
  §2). The warning goes in the XML doc and in the doc comment of `EmbeddingOptions.TaskType`.
  Decide whether `EmbedContentAsync` should also log a one-time warning when `TaskType` is set
  against a model whose name matches the `gemini-embedding-2` naming. This would mirror the existing
  "warn once per unpriced model" pattern of `GeminiCostGovernor`. It would not hardcode a model
  allow-list, which is this library's standing policy (see the doc comment of
  `GeminiConstants.Models`).

Out of scope for this change:
- Generalizing `FileObject` into a general-purpose media type. Today the constructor validates it as
  image-only. This is the second feature this session to hit that rough edge (STT hit it too, see
  `PLAN-stt.md` §2). Redesigning it is a bigger, separate, cross-cutting change. It would also touch
  `GenerateWithImageAsync`, `StreamWithImageAsync`, and `CountTokensWithImageAsync`. This plan follows
  the STT precedent and uses plain `byte[] data, string mimeType` parameters and not `FileObject`.
  The redesign deserves a dedicated pass later. We will track it as a new backlog item in
  `ROADMAP.md` once two features have independently needed it.
- Client-side enforcement of Google's documented per-input limits (6 images, 120s video, and so
  on). We have not confirmed them live (see §2). This library's standing policy is to let the API
  be the source of truth on limits that Google can change without a library update.
- Video-specific and PDF-specific convenience methods beyond the generic `byte[] data, string
  mimeType` overload. The generic overload covers them. The mechanism is the same one that we
  confirmed for two other modalities. These do not need modality-specific methods the way
  `TranscribeAsync` and `GenerateAudioAsync` needed feature-specific methods. Those call different
  endpoints. Here, one endpoint serves every modality.
- `GeminiEmbeddingGenerator` (the `Microsoft.Extensions.AI`
  `IEmbeddingGenerator<string, Embedding<float>>` adapter). Its abstraction takes text input by
  design. Multimodal embedding input has no natural fit there, and M.E.AI has no multimodal
  embedding generator abstraction as of this writing. Leave it unchanged. This feature lives on
  `IEmbeddingService` directly, in the same way that `ITranscriptionService` lives outside
  `IGeminiService`.

## 4. Architecture

### 4.1 `RequestFactory` changes (`Infrastructure/Factories/RequestFactory.cs`)

`CreateEmbeddingContent` and `CreateEmbeddingRequest` currently hardcode a single text `Part`. Add an
overload that builds a `Content` from an optional text string plus optional inline or file media:

```csharp
public static Content CreateEmbeddingContent(string? text, InlineData? media = null, FileData? fileData = null)
{
    var parts = new List<Part>();
    if (!string.IsNullOrEmpty(text)) parts.Add(new Part { Text = text });
    if (media is not null) parts.Add(new Part { InlineData = media });
    if (fileData is not null) parts.Add(new Part { FileData = fileData });
    return new Content { Role = "user", Parts = parts };
}
```
We decide the exact signature at implementation time. The point is one `Content` builder that both
the single-embed and batch paths reuse, as they do today.

### 4.2 `IEmbeddingService` and `EmbeddingService` new methods

```csharp
Task<EmbedContentResponse> EmbedContentAsync(
    string model, byte[] mediaBytes, string mimeType, string? text = null,
    EmbeddingOptions? options = null, CancellationToken cancellationToken = default);

Task<EmbedContentResponse> EmbedFileAsync(
    string model, string fileUri, string mimeType, string? text = null,
    EmbeddingOptions? options = null, CancellationToken cancellationToken = default);
```
Both methods validate `mimeType` and the media in the same way as `TranscriptionService`. They
throw `ArgumentException` for missing or empty input. They do not pre-validate the mime type
against an allow-list. `text` is optional. We confirmed live that audio-only input, with no text
part, still returns a valid embedding.

`BatchEmbedContentAsync` needs a parallel multimodal overload. Weigh these candidates at
implementation time:
- A new `EmbeddingInput` record (`{Text, MediaBytes, MimeType}`), taken as
  `IReadOnlyList<EmbeddingInput>`.
- A lower-level overload that takes `Content[]` directly. Power users who want multi-part
  aggregation within one entry would use it. We confirmed live in §2 that multiple parts in one
  `Content` produce one aggregated embedding. This is a real, intentional feature and not an
  accepted quirk.

We lean toward exposing both. A friendly `EmbeddingInput` overload serves the common case. The
`Content[]` overload stays available as an escape hatch for aggregation. This mirrors how
`ChatAsync(IList<Content>)` sits next to the simpler `ChatAsync(MessageObject[])`, for the same
reason: a simple path plus a full-control escape hatch.

### 4.3 `EmbeddingOptions.TaskType` doc comment

Add the live-confirmed warning from §2 directly on the property, as well as in this plan. The API
silently accepts and silently ignores `TaskType` on `gemini-embedding-2`. An identical-output A/B
test confirmed that, in addition to Google's docs. Cross-reference it from the XML doc
of `EmbedContentAsync` too. A caller is more likely to read that before they hit the problem.

### 4.4 Cost governance and telemetry

We expect no changes. Embedding usage already flows through the same `PostAsync` and
`UsageMetadata` path as every other `generateContent`-family call. (Embeddings do not use the
Interactions API, unlike STT.) Multimodal input tokens should surface the same way as image and
audio input tokens elsewhere in this library.

Before shipping, confirm live the `usageMetadata` shape on a multimodal embedding response. We did
not capture it in this session's research, because the raw REST probes above did not inspect
`usageMetadata`. If the per-modality breakdown is present, no plumbing changes are needed. That is
the same as the STT finding for `promptTokensDetails`.

## 5. Naming and design decisions worth recording

- Use `byte[] mediaBytes, string mimeType` and not `FileObject`. See §3. This matches the STT
  precedent (`TranscribeAsync(byte[] audioBytes, string mimeType, ...)`) for the same reason.
  `FileObject` is validated as image-only today, and generalizing it is a separate, bigger change.
- Do not enforce Google's per-input limits client-side. See §3. This is consistent with the
  library's standing policy: the API is the source of truth on limits that it can change without
  warning. We already apply this policy to model names and voice names.
- Give `TaskType` a loud warning and not a silent pass-through. We confirmed live that the API
  accepts the field, ignores it, and returns no error (§2). A caller could easily hit this and never
  learn that their task-type tuning had no effect. At implementation time, decide whether this
  deserves an actual runtime warning (logged once, mirroring the unpriced-model warning of
  `GeminiCostGovernor`) or stays doc-only. We lean toward a runtime warning, because the real
  failure mode is so silent.

## 6. Open items (updated after implementation)

- [x] PDF input. Resolved. We live-tested it twice. The first test was raw REST during the
      implementation pass (a hand-built minimal one-page PDF, `HTTP 200`, `3072`-dimension
      embedding). The second went through the actual typed client
      (`MultimodalEmbeddingLiveTests.EmbedContentAsync_Pdf_ReturnsValidEmbedding`).
- [ ] Video input. Still open, for an infrastructure reason and not a design doubt. Three different
      public sources for a small real video file all failed to connect from this session's sandbox.
      The sources were a raw GitHub test asset, a public GCS sample bucket, and Wikimedia Commons.
      They returned `HTTP 404`, `HTTP 403`, and `HTTP 000` (connection failure) respectively. An
      outbound network restriction in this specific environment caused this. It was not a
      reachability problem with those sources in general. This library also has no video-generation
      feature of its own to produce test content, the way TTS and image generation did for audio and
      images. The mechanism (`inlineData` or `fileData` through the same endpoint) is identical to
      the confirmed image and audio paths. So this is a coverage gap and not an open design question.
      Before you treat this as fully closed, verify it live with a real video file in an environment
      that can reach one.
- [ ] The `usageMetadata` shape on a multimodal embedding response. We have still not inspected it.
      This change did not touch the existing cost-governance and telemetry plumbing. We assume it
      already handles these responses the same way as every other `generateContent`-family response.
      We never checked that assumption against the real response body of a multimodal embedding call
      this session.
- [ ] Whether `outputDimensionality` truncation renormalizes to unit length, as Google's docs claim.
      We have still not checked it. We confirmed only the vector length and not its norm.
- [ ] The documented per-modality limits (6 images, 120s video, 180s audio, 1 PDF of 6 pages). We
      have still not tested them against the real boundary. This is low priority, given the decision
      in §3 not to enforce them client-side.

## 7. Tests added

Unit tests (`tests/Junaid.GoogleGemini.Net.Tests/EmbeddingTests.cs`), based on
`FakeHttpMessageHandler`, 13 tests:
- [x] `EmbedContentAsync(model, bytes, mimeType, text)` sends the correct multi-part `Content` shape
      (a text part and an inlineData part). It omits the text part when `text` is null.
- [x] `EmbedFileAsync` sends `fileData`, not `data`.
- [x] The batch `EmbeddingInput` overload sends one `Content` per input. Each can carry non-text
      parts. The overload maps each response embedding back to its input in order.
- [x] Validation: missing or empty media, a missing file URI or MIME type, an `EmbeddingInput` with
      neither text nor media, and an `EmbeddingInput` that sets both `MediaBytes` and `FileUri`. All
      throw `ArgumentException` before any network call.
- [x] `EmbeddingOptions.TaskType` combined with `gemini-embedding-2` logs exactly one warning across
      multiple calls (not one per call). Combined with an older model (`gemini-embedding-001`), it
      logs nothing. These tests are also the first unit test coverage that `EmbeddingService` has
      ever had, for any of its methods, including the text-only ones.

Live tests (`tests/Junaid.GoogleGemini.Net.IntegrationTests/MultimodalEmbeddingLiveTests.cs`),
`[RequiresGeminiKey]`, 6 tests:
- [x] Image input (generated with this library's own image generation) returns a real,
      non-degenerate embedding. The test asserts that the cosine similarity against a text-only
      control lands in `(0.1, 0.95)`. This mirrors the `0.528` from research. It does more than
      check that no exception was thrown.
- [x] Audio input (generated with this library's own TTS), with no text part, returns a valid
      embedding.
- [x] File-based (Files API URI) embedding succeeds.
- [x] PDF input (the hand-built PDF from §6) returns a valid embedding.
- [x] A batch call that mixes a text-only input and a multimodal input returns correctly ordered,
      distinct embeddings.
- [x] `TaskType` has zero effect on `gemini-embedding-2`. We confirmed it again through the typed
      client (byte-for-byte identical `Embedding.Values` with and without it set). The raw-REST
      confirmation from research came first.
- [ ] Video input: not added, per the open item in §6.
