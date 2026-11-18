# Plan: Gemini 4 and the next releases (6.9.0 onward)

Status, 2026-11-18: research and live probes are done. No library code is written yet. `6.8.1`
shipped on 2026-11-16. Every fact below was checked on 2026-11-16 or 2026-11-18 against Google's raw
pages, Microsoft's release metadata, NuGet, or a live call. Section 9 lists what is still open.

This plan replaces section 9 of `PLAN-gemini-landscape-2026-11.md` from step 2 on. Where the two
differ, this file is newer.

## 1. Deadlines

| Date | What happens | Library status |
|---|---|---|
| 10 Nov 2026 (passed) | .NET 8 and .NET 9 reached end of support. .NET 10 (LTS) and .NET 11 (STS) are current, both until 14 Nov 2028. | The library targets `net8.0`, `net9.0` and `netstandard2.0`. It still works on .NET 10 and 11. See section 6.8. |
| 1 Dec 2026, 00:00 UTC | Gemini 4 Carbon and Argon double in price: $4 input, $20 output, $0.20 cached. | Not handled. The governor has no Gemini 4 prices. **This drives 6.9.0.** |
| 1 Dec 2026 | `gemini-2.5-pro` and `gemini-2.5-flash` route to 3.8 Flash and 3.5 Flash-Lite. | Handled in 6.8.1. |
| 18 Dec 2026 | The three old TTS models shut down. | Handled in 6.8.0 and 6.8.1. |
| 1 Jan 2027 | 3.6 Flash, 3.8 Flash and the 3.8 TTS models lose their introductory price. 3.8 Flash then costs the same per token as Hydrogen. | Handled in 6.8.0. It also reopens the default-model decision (section 8). |

## 2. What changed since 16 November

1. **The drift watch worked.** Its first scheduled run opened issue #65. Google's discovery document
   moved from revision 20261113 to 20261116 and added media-slicing fields. The deprecations page did
   not change.
2. **18 Nov: Office files and slicing.** You can now send Word, PowerPoint, Excel and OpenDocument
   files directly. You can also slice any document, spreadsheet, video or audio part by page range,
   worksheet, cell range, time interval or content layer. Gemini bills only the slice. Section 6.3.
3. **17 Nov: voice editing in `gemini-3.5-transcribe`.** It needs smart transcription mode, which this
   library does not expose. Section 6.4.
4. **Google now recommends Hydrogen as the default.** Its new "Choosing a model" page says "Default to
   `gemini-4-hydrogen`" and calls it the recommended upgrade from 3.8 Flash. Section 8.
5. **Microsoft shipped .NET 11 and `Microsoft.Extensions.*` 11.0.0** (10 to 11 Nov). The 11.x platform
   packages, for example `Microsoft.Extensions.Http`, no longer ship `net8.0` or `net9.0` builds.
   `Microsoft.Extensions.AI.Abstractions` 11.0.0 still does.

## 3. Gemini 4: verified facts

| Model | Tier | Default thinking | Levels | Price per 1M tokens (input / output / cached) |
|---|---|---|---|---|
| `gemini-4-hydrogen` | Free and paid | `high` | low, medium, high, xhigh | $1.50 / $7.50 / $0.075 |
| `gemini-4-carbon` | Paid only | `medium` | low, medium, high | $2 / $10 / $0.10 until 30 Nov, then $4 / $20 / $0.20 |
| `gemini-4-argon` | Paid only | `medium` | low, medium, high | Same as Carbon |

- All three take text, image, video, audio, PDF and now Office files. All three output text only.
- Context window: 1,048,576 tokens, shared by input, thinking and output. Max output: 65,536 by
  default, up to 1,048,576 with long decode.
- Google gives no time zone for the 1 Dec change. It raises the price, so we switch at the earliest
  plausible time, 00:00 UTC. That is the same rule as the 2027 change.
- Explicit caching is not supported. Implicit caching is on for prompts of 4,096 tokens or more and
  takes 95% off cached input.
- `temperature`, `topP` and `topK` are ignored. `minimal` is rejected on all three.
- Every token is billed, including thinking and the instructions that grounding adds.
- On Gemini 4, an Office document with no slicing is read as page images (about 560 tokens a page).
  Gemini 3 reads it as text. The same file can cost more on Gemini 4.

### Long decode on generateContent

- A hop that reaches its cap ends with `finishReason: CONTINUATION` and a `continuationToken`. To
  continue, send the same request again with the token and the same `maxOutputTokens`. Do not add the
  partial output to `contents`.
- `maxOutputTokens` is the total for all hops. A hop can be all thinking and have empty text.
- On REST, each response reports only its own hop. Add every usage field across hops for cost. The
  input of each hop includes everything generated so far, and implicit caching discounts it 95%.
- Google's SDKs continue by themselves (.NET SDK from 1.26.0). This library does not.

### The API surface (discovery revision 20261116)

- Request: `continuationToken`, `serviceTier`, `store`, `labels`.
- Candidate: `continuationToken`, `finishMessage`, `citationMetadata`.
- Response: `modelStatus` (`modelStage`, `retirementTime`, `message`), `responseId`.
- Usage: `serviceTier`, `cacheTokensDetails`.
- Part: `documentMetadata`, `spreadsheetMetadata`, `audioMetadata`, plus `videoMetadata` and
  `mediaResolution`. `FunctionResponsePart` has the same five.

## 4. Live results (2026-11-16, run 43819638043)

| Probe | Result |
|---|---|
| "Reply OK" through the library, on each model | HTTP 200 on all three. `modelVersion` equals the model ID. |
| `maxOutputTokens` 32,768, 40,000 and 262,144 on Carbon. 40,000 on Argon. 40,000 and 100,000 on Hydrogen | HTTP 200 on all. |
| `minimal` on Hydrogen and on Carbon | HTTP 400: "Thinking level MINIMAL is not supported for this model." |
| `xhigh` on Carbon and on Argon | HTTP 400: "Thinking level XHIGH is not supported for this model." |
| `xhigh` on Hydrogen | HTTP 200. |
| `thinkingBudget` with `thinkingLevel` on Carbon | HTTP 400: "You can only set only one of thinking budget and thinking level." |
| `temperature`, `topP`, `topK` on Carbon | HTTP 200. Ignored. |
| Explicit caching for Hydrogen and for Carbon | HTTP 400: "Explicit caching is not supported for models/gemini-4-..." |
| Continuation, Carbon | Inconclusive. The model refused the prompt after 1,006 tokens. |

## 5. Corrections

| Earlier claim | What is true |
|---|---|
| (Landscape plan) Non-streaming Carbon or Argon fails with 400 above 32,768 output tokens. | Google's prose says so, and its own REST example does the opposite. Live, every value we tried returned 200. We will not guard against it. A guard would block valid calls. |
| (Landscape plan) All three models work on `generateContent`. | Confirmed live. |
| (Landscape plan) `store` is on by default for new paid projects. | Confirmed. Google's changelog, 26 Oct 2026. The opt-out is `store=false` on each request, or a dashboard switch. |
| (Landscape plan) The error mapping is unverified for new codes. | Every non-success status becomes `GeminiApiException` with the status and Google's error. Nothing is lost. Low risk. |
| (First draft of this plan) The library should guard before the call. | No. Google's 400 messages are clear and exact. A guard could go stale or block a valid call. |
| (This review, first reading) The retry handler lacks jitter and 408. | Only on `netstandard2.0`. On .NET 8 and later, the main and Batch clients use Microsoft's resilience handler, with jitter and 408. |

## 6. Gaps in the library today

1. **No Gemini 4 constants or prices.** A Gemini 4 call works, but the governor logs one warning and
   does not count its spend. A budget is blind to Gemini 4. After 1 Dec, the error on Carbon and Argon
   is $4 and $20 per 1M tokens.
2. **No `Store` option.** A caller on a new paid project cannot keep requests out of Google's logs
   from code. This is a privacy gap with a small fix.
3. **No slicing fields.** `Part` has no `documentMetadata`, `spreadsheetMetadata`, `audioMetadata` or
   `videoMetadata`, and no per-part `mediaResolution`. `videoMetadata` (start and end offset, frame rate)
   has been missing for longer than the new fields. Slicing cuts cost, so it fits the library's cost
   story.
4. **No transcription `Mode`.** `PLAN-stt.md` left it out on purpose until there was a real reason.
   Voice editing (`source_text`) is that reason. It only works in smart mode.
5. **`FileUtilities.GetMimeType` is incomplete.** It returns `application/octet-stream` for every
   Office type. It has no entry for `.aiff`, `.ogg`, `.mpeg`, `.mpg`, `.flv`, `.wmv` or `.3gp`, which
   Google lists as supported. It maps `.avi` to `video/x-msvideo`, and Google's list says `video/avi`.
   We have not tested whether Google accepts `video/x-msvideo`. The method is public.
6. **The `netstandard2.0` retry handler** has no jitter and does not retry 408. Google's guidance asks
   for both. This affects .NET Framework, Mono and Unity users only.
7. **The `IChatClient` adapter has two unit tests.** Its real coverage is in the live suite. It compiles
   and passes against `Microsoft.Extensions.AI.Abstractions` 11.0.0 (checked locally on 2026-11-18),
   so users who upgrade are not broken.
8. **Targets.** All three packages target `net8.0` and `net9.0`, both out of support. Nothing breaks:
   the `net8.0` build runs on .NET 10 and 11. No code uses a .NET 9 symbol, so the `net9.0` build adds
   nothing. Do not drop `net8.0` yet. Without it, .NET 8 and 9 users would get the `netstandard2.0`
   build and lose the Microsoft resilience handler.
9. **No continuation loop.** On a hard prompt, Carbon or Argon can end a hop with empty text.
   `GetTextOrThrow()` then throws. Not seen live yet.

## 7. Release plan

### 6.9.0: Gemini 4 and storage opt-out (ship by 25 Nov)

Goal: correct budgets before Carbon and Argon double on 1 Dec. Users need a few days to upgrade.

1. Constants for the three models. `ThinkingLevels.XHigh`, documented as Hydrogen only. Add the
   three to `ContentGenerationModels`.
2. Governor prices. Carbon and Argon switch at 00:00 UTC on 1 Dec. Hydrogen is flat.
3. `GeminiRequestOptions.Store` (nullable bool), sent as `store`. A live test that `store: false` is
   accepted.
4. `GetMimeType`: add the ten Office and OpenDocument types and the seven missing media types. Test
   `.avi` live with both MIME types before changing it.
5. Docs: thinking levels per model, no explicit caching, ignored sampling fields, long timeouts (6.8.1
   allows up to an hour), and how Office files are read on Gemini 4.
6. Tests. Unit tests for the prices, the switch and the new option. Live tests that pin section 4
   (these replace the probes). A live check that the governor's estimate matches Google's usage.
7. Review #65, then refresh the drift snapshots and close it.

Not in 6.9.0: no pre-call guards, and no change to the default model.

### 6.10.0: process less, pay less (December)

1. Slicing on `Part` and `FunctionResponsePart`: `DocumentMetadata`, `SpreadsheetMetadata`,
   `AudioMetadata`, `VideoMetadata` and per-part `MediaResolution`. Live tests that the billed tokens
   drop when you slice.
2. Transcription `Mode` (verbatim or smart) and `SourceText` for voice editing. Re-check the request
   shape live first, as `PLAN-stt.md` did.
3. `netstandard2.0` retry parity: jitter and 408.
4. More unit tests for the `IChatClient` adapter.

### 6.11.0: platform and default model (January)

1. Add a `net10.0` target to all three packages and run the tests on .NET 10 and 11. Remove the
   `net9.0` target. Keep `net8.0` as the floor and `netstandard2.0`. The build machine has only the
   .NET 9 SDK, so this needs the .NET 10 SDK installed, or CI only.
2. The default-model decision (section 8), with live cost data.
3. The continuation loop, with the governor adding every hop, if a real `CONTINUATION` hop has been
   seen by then.

### Later, one at a time

`ServiceTier` (Flex is 50% cheaper, and the governor must price each tier), `Labels`,
`ModelStatus` with a log warning before retirement, the Decisions API (`gemini-decision-1`), a
webhook receiver for Batch, and the bigger bets in the landscape plan (Interactions client, Live API,
Vertex).

## 8. Decisions for the maintainer

1. **The default model.** Today `Defaults.Model` and `Recommended` are `gemini-3.8-flash`. Google now
   says to default to Hydrogen. Google's own claim is that Hydrogen makes up facts more than 5 times
   less often than 3.8 Flash. From 1 Jan the two cost the same per token. But Hydrogen thinks at `high`
   by default, and 3.8 Flash at `medium`, so the same call can use more tokens. The default model is
   used by every caller who does not pick one. My advice: keep 3.8 Flash in 6.9.0. In January, run the
   same prompts on both models, compare tokens and cost, and decide in 6.11.0 with release notes.
2. **Targets.** Section 7, 6.11.0. Needs your OK to install the .NET 10 SDK on this machine, or we
   build and test that target in CI only.

## 9. Still open

- A real `CONTINUATION` hop. The first probe prompt was refused. A run that produces more than 32K
  tokens costs up to $0.70 on Carbon before 1 Dec, about twice that after.
- Whether `store: false` is accepted on `generateContent` in practice. The discovery document has the
  field. 6.9.0 tests it live.
- The exact token savings of slicing. 6.10.0 measures it.

## 10. Sources

Google, fetched on 2026-11-16 and 2026-11-18 from `https://aistudio.google.com/docs/`: the three
model pages, the three "What's new in Gemini 4" pages, `choosing-a-model.md`, `long-decode.md`
(both variants), `pricing.md`, `thinking.md`, `changelog.md`, `deprecations.md`,
`document-processing.md` (both variants), `transcribe.md` and `troubleshooting.md`. The discovery
document diff is in issue #65. Microsoft: `releases-index.json` from the .NET release metadata feed,
and the NuGet package index for `Microsoft.Extensions.*` and `Google.GenAI` (2.2.0). Live calls:
GitHub Actions run 43819638043.
