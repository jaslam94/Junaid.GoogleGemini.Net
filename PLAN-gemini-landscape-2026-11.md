# Gemini landscape report and next steps (15 November 2026, second pass)

This file records what changed in the Gemini world since the 6.4.1 audit (15 August 2026). It also
says where the library stands today and what to do next.

The library is at 6.8.0. The last push to the repo was 13 October 2026. In the month since, Google
shipped the whole Gemini 4 family and several new APIs.

This is the second pass. The first pass could not read Google's docs. This pass read them.

---

## 1. Corrections to the first pass

The first pass used search snippets and a third-party copy of Google's migration notes. Reading the
real pages changed these claims.

| First pass said | Verified now | Source |
|---|---|---|
| `gemini-3.1-flash-image` shuts down on 29 Oct 2026. It may already be gone. This was risk number 1. | Google deprecated it on 6 Oct. The deprecations table says "No shutdown date announced". The 29 Oct date came from a copy in the `gemini-skills` repo and is wrong. The 6.8.0 notes were right. This risk is gone. | [Google doc] |
| The 3.1 TTS preview and the 2.5 TTS models shut down on 17 Nov. | All three shut down on **18 Dec 2026**. The library's `[Obsolete]` messages say 17 Nov. They are wrong now. | [Google doc] |
| The old Live preview models shut down on 17 Nov. | `gemini-3.1-flash-live-preview` has no shutdown date. `gemini-2.5-flash-native-audio-preview-12-2025` shuts down on 31 Mar 2027. | [Google doc] |
| `gemini-2.5-*` has no shutdown date. | `gemini-2.5-pro` and `gemini-2.5-flash` are deprecated. On **1 Dec 2026** Google routes API calls to `gemini-3.8-flash` and `gemini-3.5-flash-lite` and bills at the new model's rate. `gemini-2.5-flash-lite` is not deprecated. | [Google doc] |
| Hydrogen price was "one tracker says $1.50 and $7.50". | Confirmed on the pricing page: $1.50, $7.50, cache $0.075. | [Google doc] |
| Computer Use is Preview. | Computer Use became GA on 30 Oct 2026. | [Google doc] |
| The Interactions client needs `Api-Revision`. | Google removed the legacy schema on 8 Jun and now ignores the header. Risk is low. | [Google doc] |
| Flex and Priority work only on Interactions. | They also work on `generateContent` through a top-level `serviceTier` field. | [Google doc] |
| Gemini 4 may need the Interactions API. | All three Gemini 4 models work on `generateContent`. Long output uses a `continuationToken`. | [Google doc] |

---

## 2. How to read this report

| Label | Meaning |
|---|---|
| [Google doc] | Read from `aistudio.google.com/docs/*.md` on 15 Nov by fetch agents. Some long pages were cut at 40,000 characters. Where an agent could not read the tail, I mark it "(summary)". |
| [Google repo] | Read from Google's GitHub repos with `gh`: `gemini-skills`, `dotnet-genai`, `python-genai`. |
| [Discovery] | Google's public discovery document, revision 20261113, downloaded with `curl`. |
| [Repo] | Read in this repository, its CI history, or the NuGet search API. |
| [Third party] | A blog, forum post or price tracker. Treat it as a lead. |

Limits of this method:

- Fetch agents write summaries. I copied numbers and IDs as they reported them. They are likely
  right but not certain. Re-check prices and dates before you ship code that depends on them.
- I made no live API calls. No key was set in this shell.
- Google's own pages disagree in places. Section 10 lists the conflicts.

---

## 3. Summary

1. **Gemini 4 is here:** `gemini-4-hydrogen`, `gemini-4-carbon` and `gemini-4-argon`. All work on
   `generateContent`. The library has no support for them.
2. **Gemini 4 changes the rules.** No explicit caching. No `temperature`, `top_p` or `top_k`. A new
   `xhigh` thinking level on Hydrogen. A non-streaming REST call to Carbon or Argon fails with HTTP
   400 if `maxOutputTokens` is above 32,768. Long output needs a continuation loop.
3. **Argon and Carbon prices double on 1 Dec.** The cost governor has no Gemini 4 prices.
4. **The library cannot set a long timeout.** Its validator rejects `TimeoutSeconds` above 300.
   Google tells Flex and long-decode users to allow 10 to 20 minutes.
5. **Two dates in the library are wrong.** The old TTS models shut down on 18 Dec, not 17 Nov.
   `gemini-2.5-pro` and `gemini-2.5-flash` change on 1 Dec.
6. **Interactions is Google's default API.** `generateContent` is "legacy" but fully supported. New
   agents, Omni video, Decisions and Live features ship on the newer surfaces.
7. **New APIs:** Decisions, managed agents with environments, credentials and cron triggers,
   Webhooks, Flex and Priority tiers, `gemini-omni-1.2-flash` video, Lyria music, Live API on 3.8.
8. **Google changed account-level defaults.** `store` is on by default for new paid projects (26
   Oct). SDKs default to API `v1`. The billing model changed to Prepay and Postpay.
9. **The library silently drops at least 50 fields** that Google sends or accepts. Section 8 lists
   them.
10. **The official .NET SDK moved fast** (2.1.0, weekly releases, 4.38M downloads) but still lacks
    Decisions, thinking summaries and a speech-to-text adapter. Our library covers more of Google's
    newer speech, batch and cost features than it does.

---

## 4. Calendar

| Date | What happens | Source |
|---|---|---|
| 30 Nov 2026 | Gemini 4 Argon and Carbon introductory price ends. | [Google doc] |
| 1 Dec 2026 | Argon and Carbon price rises to $4 and $20. `gemini-2.5-pro` routes to `gemini-3.8-flash`. `gemini-2.5-flash` routes to `gemini-3.5-flash-lite`. Both bill at the new rates. | [Google doc] |
| 3 Dec 2026 | `gemini-omni-1.1-flash` routes to `gemini-omni-1.2-flash` at 1.2 rates. | [Google doc] |
| 18 Dec 2026 | `gemini-3.1-flash-tts-preview`, `gemini-2.5-flash-preview-tts` and `gemini-2.5-pro-preview-tts` shut down. | [Google doc] |
| 31 Dec 2026 | Introductory price ends for `gemini-3.6-flash`, `gemini-3.8-flash`, the 3.8 TTS models and `gemini-robotics-er-2-preview`. The 6.8.0 governor already handles most of these. | [Google doc] [Repo] |
| 12 Jan 2027 | Per Google Cloud notes, Hydrogen and later models get cache control with TTL. I do not know if this covers the Gemini API. | [Third party] |
| 15 Mar 2027 | `gemini-2.5-flash-image` shuts down. | [Google doc] |
| 31 Mar 2027 | `gemini-2.5-flash-native-audio-preview-12-2025` shuts down. | [Google doc] |
| 7 May 2027 | `gemini-3.1-flash-lite` shuts down. Replacement: `gemini-3.5-flash-lite`. | [Google doc] |
| 14 May 2028 | `gemini-embedding-001` shuts down. Replacement: `gemini-embedding-2`. | [Google doc] |

---

## 5. What is new

### 5.1 Gemini 4

| | `gemini-4-hydrogen` | `gemini-4-carbon` | `gemini-4-argon` |
|---|---|---|---|
| Released | 12 Nov | 10 Nov | 13 Oct |
| Role | Fast. Google's default for most workloads. | Most capable. | Efficient frontier. |
| Thinking levels | `low`, `medium`, `high` (default), `xhigh` | `low`, `medium` (default), `high` | `low`, `medium` (default), `high` |
| Free tier | Yes | No | No |
| Price per 1M (in / out / cache) | $1.50 / $7.50 / $0.075 | $2 / $10 / $0.10 until 30 Nov, then $4 / $20 / $0.20 | Same as Carbon |
| Batch and Flex per 1M | $0.75 / $3.75 | $1 / $5, then $2 / $10 | Same as Carbon |
| Priority per 1M | $2.70 / $13.50 | $3.60 / $18, then $7.20 / $36 | Same as Carbon |

Shared rules [Google doc]:

- Context: 1,048,576 tokens, shared by input, thinking and output.
- Default output cap: 65,536 tokens.
- On `generateContent`, a **non-streaming REST call** to Carbon or Argon returns HTTP 400 if
  `maxOutputTokens` is above 32,768 and no continuation token is set. Streaming and SDK calls allow
  65,536 per hop. Hydrogen allows 65,536 for all of them.
- Long decode (up to 1,048,576 tokens minus input) works two ways:
  - On `generateContent`: a loop on `finishReason: "CONTINUATION"` with a top-level
    `continuationToken`. Resend the same request each hop. Keep `maxOutputTokens` constant. Join the
    parts. Official SDKs do this automatically (.NET SDK 1.26.0 and later).
  - On Interactions: `background: true`, then poll. Runs up to 24 hours.
- Every hop is billed. The prompt tokens repeat on each hop, with a 95 percent cache discount.
- `temperature`, `top_p`, `top_k` and `candidate_count`: ignored on Argon. Hydrogen ignores
  in-range values and returns 400 for out-of-range values.
- Thinking cannot be turned off. `minimal` is rejected. `xhigh` exists on Hydrogen only.
- `thinkingBudget` alone maps to the nearest level with a warning. Combined with `thinkingLevel` it
  returns 400.
- Explicit caching (`cachedContents`) is not supported. The error text is not documented. Implicit
  caching starts at 4,096 tokens and gives a 95 percent discount. In multi-turn use, a new request
  must add at least 2,050 tokens to cache the full previous request.
- Supported: Search, Maps, code execution, URL context, File Search, function calling, computer use,
  structured output, Batch, Flex, Priority.
- Not supported: Live API, audio and image generation.
- Raise HTTP read timeouts to at least 15 to 20 minutes.
- Hydrogen note: do not ask for raw-text JSON tool calls without declared tools. That can return
  `MALFORMED_FUNCTION_CALL`.
- Hydrogen thinks at `high` by default. One forum user reports 4x cost per call against 3.8 Flash.
  That is one report. [Third party]

Other model facts [Google doc]:

| Model | Facts |
|---|---|
| `gemini-3.8-flash` | Stable. `minimal` returns an error. Thinking `low`, `medium` (default), `high`. Input 1M, output 65,536. Free tier. |
| `gemini-3.6-flash` | Stable. A last non-empty model turn returns HTTP 400. `temperature`, `top_p`, `top_k` are ignored. |
| `gemini-3.5-flash-lite` | Stable. $0.30 / $2.50. Default thinking `minimal`. |
| `gemini-3.7-flash`, `gemini-3.5-flash` | Deprecated 8 Oct. Google routes them to 3.8 and 3.6. |
| `gemini-3.1-pro-preview` | Still the Pro model. $2 / $12 up to 200k tokens, $4 / $18 above. |
| `gemini-flash-latest` and similar aliases | Exist on `v1beta` only. Not on `v1`. |

### 5.2 Interactions API is the default

[Google doc]

- Since June 2026 Google calls it "Generally Available and the recommended interface for all new
  projects". `generateContent` is "legacy" but "remains fully supported". No retirement plan appears
  on any page.
- Google says new models, agentic features and tools "will launch on the Interactions API".
- Endpoint: `POST /v1beta/interactions`. Also `GET /{id}`, `DELETE /{id}` and `POST /{id}/cancel`.
- The legacy schema (`outputs`) was removed on 8 Jun 2026. The `steps` schema is the only one.
  `Api-Revision` is ignored since then. Google's samples still send `2026-05-20`.
- Statuses: `in_progress`, `requires_action`, `completed`, `failed`, `cancelled`, `incomplete`,
  `requires_continuation`. The polling samples also check `queued`.
- Step types: `user_input`, `model_output`, `thought`, `function_call`, `function_result`,
  `google_search_call/result`, `google_maps_call/result`, `code_execution_call/result`,
  `url_context_call/result`, `file_search_call/result`, `mcp_server_tool_call/result`,
  `processing_call/result`.
- Streaming events: `interaction.created`, `interaction.status_update`, `step.start`, `step.delta`,
  `step.stop`, `interaction.completed`, `error`, then `[DONE]`. A stream can resume with
  `?stream=true&last_event_id=`.
- Storage is on by default. Paid tier keeps data 55 days. Free tier keeps it 1 day.
- Not supported on Interactions: Batch API, explicit caching, automatic function calling, remote MCP
  on Gemini 3.
- The error body is `{"error":{"code":"<snake_case>","message":"..."}}`. Section 5.12 lists the codes.
- `tools`, `system_instruction` and `generation_config` apply to one interaction. Send them again.
- Model IDs in the OpenAPI enum include Gemma 4, all Gemini 3 and 4 models, image, TTS, Lyria and
  Omni models.

### 5.3 Pricing (per 1M tokens, standard tier)

[Google doc] Dates with arrows read "until the date, then after it".

| Model | Input | Output | Cached input | Notes |
|---|---|---|---|---|
| `gemini-4-hydrogen` | $1.50 | $7.50 | $0.075 | Free tier. Priority $2.70 / $13.50. |
| `gemini-4-carbon`, `gemini-4-argon` | $2 to $4 | $10 to $20 | $0.10 to $0.20 | Change on 1 Dec. No free tier. |
| `gemini-3.8-flash`, `gemini-3.6-flash` | $0.75 to $1.50 | $3.75 to $7.50 | $0.075 to $0.15 | Change on 1 Jan 2027. Free tier. Priority $1.35 to $2.70 in. |
| `gemini-3.5-flash-lite` | $0.30 | $2.50 | $0.03 | Priority $0.54 / $4.50. |
| `gemini-3.1-flash-lite` | $0.25 (audio $0.50) | $1.50 | $0.025 | Shuts down 7 May 2027. |
| `gemini-3.1-pro-preview` | $2 (over 200k: $4) | $12 (over 200k: $18) | $0.20 | Storage $4.50 per 1M tokens per hour. |
| `gemini-2.5-pro` | $1.25 / $2.50 | $10 / $15 | $0.125 | Routed on 1 Dec. |
| `gemini-2.5-flash` | $0.30 | $2.50 | $0.03 | Routed on 1 Dec. |
| `gemini-2.5-flash-lite` | $0.10 | $0.40 | $0.01 | Not deprecated. |
| `gemini-3.8-live` family | text $0.75, audio $3, video $1 | text $4.50, audio $12 | none | Free tier. |
| `gemini-3.5-transcribe` | audio $2 | text $12 | none | About $0.005 per minute. |
| `gemini-3.5-transcribe-live` | audio $3.50 | text $21 | none | |
| `gemini-3.5-live-translate-preview` | audio $3.50 | audio $21 | none | |
| `gemini-3.8-flash-tts` | text $0.50 to $1 | audio $9 to $18 | none | 25 audio tokens per second. |
| `gemini-3.8-flash-lite-tts` | text $0.50 to $1 | audio $6 to $12 | none | |
| `gemini-decision-1` | $0.10 | 0 tokens | none | Batch not supported. |
| `gemini-embedding-2` | text $0.20, image $0.45, audio $6.50, video $12 | none | none | Batch is half. |
| `gemini-embedding-001` | $0.15 | none | none | |

Image models:

| Model | Input | Text output | Image output | Per image |
|---|---|---|---|---|
| `gemini-nano-banana-2.1` | $1.50 | $7.50 | $30 | $0.0336 (1K), $0.0504 (2K), $0.113 (4K) |
| `gemini-3.1-flash-image` | $0.50 | $3 | $60 | $0.045 (0.5K), $0.067 (1K), $0.101 (2K), $0.151 (4K) |
| `gemini-3.1-flash-lite-image` | $0.25 | $1.50 | $30 | $0.0336 (1K) |
| `gemini-3-pro-image` | $2 | $12 | $120 | $0.134 (1K and 2K), $0.24 (4K) |

Other prices [Google doc]:

- `gemini-omni-1.2-flash`: $1.50 in, $9 text out, $12 video out. About $0.07 per second at 720p.
  360p is $0.023, 1080p is $0.104, 4K is $0.209 per second. Omni 1.1 was $17.50 video out.
- Lyria: $0.08 per full song (`lyria-3.5`, `lyria-3-pro-preview`). $0.04 per 30-second clip.
- Batch and Flex: half the standard price. Priority: 75 to 100 percent more.
- Google Search and Maps grounding on Gemini 3 and 4: 5,000 requests per month free (shared), then
  $14 per 1,000. Daily cap 1,500 per project for Search. On 2.5 models: $35 and $25 per 1,000.
- Code execution and URL context: no fee. Tokens bill as normal. File Search: free to query.
  Indexing bills at embedding rates.
- Thinking tokens bill as output. Grounding bills the hidden system instructions and function
  declarations as input.
- Agentic video understanding: up to 88 percent fewer tokens on long videos.
- Managed agents: tokens at list rates. Environment compute is free during preview.

### 5.4 Service tiers

[Google doc] Both are Preview.

| | Flex | Priority | Batch |
|---|---|---|---|
| Field | `serviceTier: "flex"` (generateContent) or `service_tier` (Interactions) | `"priority"` | separate API |
| Price | 50 percent of standard | +75 to 100 percent | 50 percent |
| Latency | 1 to 15 minutes target | seconds | up to 24 hours |
| On overload | 503 or 429. No fallback. Client must retry. | Downgrades to standard and bills standard. | Queues |
| Limits | Counts toward normal limits | 0.3x the standard limit | Own pool |
| Header or field | none documented | `x-gemini-service-tier` header | |

- `usageMetadata.serviceTier` reports the tier actually used.
- Flex clients need a timeout of 10 minutes or more. SDK samples use 900,000 ms.
- 12 models support both tiers. Gemini 4, 3.8, 3.6, 3.5 Flash-Lite, 3.1 Flash-Lite, 3.1 Pro, 3 Flash
  preview and all 2.5 text models.

### 5.5 Decisions API

[Google doc]

- `POST /v1beta/decisions`, model `gemini-decision-1`. Released 11 Nov. Status not stated.
- Body: `model`, `input` (text, image, audio, video or PDF), and `questions` (1 to 1,000). Keys match
  `[A-Za-z0-9_-]+`, up to 64 characters.
- Question types: `noul` (yes or no), `choice` (2 to 255 options) and `score` (2 to 255 levels).
  Instructions are text only.
- Answers: `noul` returns a float from 0.01 to 0.99. `choice` returns probabilities that sum to 1.
  `score` returns a probability-weighted level. Every answer has `confidence`.
- Limits: 262,144 context tokens. 0 output tokens. Inline data up to 100 MB (PDF 50 MB). URL input up
  to 15 MB. No YouTube or `gs://` URIs.
- Errors are HTTP 400 with `safety`, `prohibited_content`, `blocklist`, `image_safety` or
  `content_blocked`.
- Rate limits, free tier: 200 RPM, 50,000 TPM, 10,000 RPD. Tier 1: 1,500 RPM. Tier 3: 15,000 RPM.
- Not supported: caching, Batch, Flex, Priority, tools, thinking, structured output.
- The official .NET SDK has an open request for it (issue 553). Python and JS have it (3.2.0).

### 5.6 Managed agents

[Google doc] All Public Preview, on `v1beta`.

- **Antigravity agent** `antigravity-preview-09-2026` (released 17 Sep). Runs in a Google-hosted
  Ubuntu sandbox with Python 3.12 and Node 22. Default model Hydrogen. Context compacts at about
  135,000 tokens. Typical run 100,000 to 3M tokens, about $0.25 to $5. It supports text and image
  input only. It does not support `temperature` and similar fields, structured output, File Search,
  Computer Use or Maps.
- **Deep Research** `deep-research-preview-04-2026` and `deep-research-max-preview-04-2026`.
  Interactions only. Up to 60 minutes. About $1 to $3 (standard) and $3 to $7 (max) per task.
  Options: collaborative planning, visualization, MCP, File Search.
- **Custom agents:** `POST /v1beta/agents` with `base_agent`, `agent_config`, `system_instruction` and
  `base_environment`. Up to 1,000 agents. Reserved ID prefixes include `gemini-` and `google-`.
- **Environments:** `/v1beta/environments`. Sources: repository (500 MB), `gcs` (2 GB), inline (1 MB
  per file). Network can be `disabled` or an allowlist. Idle for 15 minutes snapshots the VM. Deleted
  after 7 days of inactivity.
- **Credentials:** `/v1beta/credentials`. Types: `bearer_token`, `oauth2`, `environment_variable`.
  A proxy injects secrets so the agent never sees them.
- **Triggers:** `/v1beta/triggers`. A cron schedule runs an agent. Defaults: 5 consecutive failures
  disable it, 600-second timeout.
- **Hooks:** `.agents/hooks.json` with `pre_tool_execution` and `post_tool_execution`. Handlers are
  `command` or `http`. They fail open.
- `antigravity-preview-05-2026` was replaced on 5 Oct. `deep-research-pro-preview-12-2025` shut down
  on 23 Oct.

### 5.7 Live API

[Google doc] Preview. WebSocket only.

- Models: `gemini-3.8-live` (GA 15 Sep), `gemini-3.8-live-extended-thinking`,
  `gemini-3.5-transcribe-live`, `gemini-3.5-live-translate-preview`.
- URL: `wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=...`.
  With an ephemeral token the method is `BidiGenerateContentConstrained` and the query is
  `access_token=`.
- First message: `{"setup": {...}}`. Wait for `setupComplete`. Setup fields: `model`,
  `generationConfig`, `systemInstruction`, `tools`, `realtimeInputConfig`, `sessionResumption`,
  `contextWindowCompression`, `inputAudioTranscription`, `outputAudioTranscription`, `proactivity`,
  `historyConfig`, `labels`.
- Client messages: `realtimeInput` (`audio`, `video`, `text`, `audioStreamEnd`, `activityStart`,
  `activityEnd`), `clientContent`, `toolResponse`. `mediaChunks` is deprecated.
- Server messages: `setupComplete`, `serverContent` (with `modelTurn`, `turnComplete`,
  `generationComplete`, `interrupted`, transcriptions, `interactionStatus`), `toolCall`,
  `toolCallCancellation`, `goAway`, `sessionResumptionUpdate`, `usageMetadata`, `voiceActivity`.
- Audio: 16 kHz 16-bit PCM in. 24 kHz out. Video up to 1 frame per second. Output is audio only.
- Limits: 15 minutes audio-only and 2 minutes with video, unless context compression is on.
  A connection lasts about 10 minutes. Resumption handles last 2 hours. Context 131,072 in, 65,536
  out. 3.8 Live has proactive audio always on.
- Tools: Google Search only. Function declarations take `behavior: NON_BLOCKING` or `BLOCKING`.
  Results use `scheduling`: `INTERRUPT`, `WHEN_IDLE` or `SILENT`.
- Ephemeral tokens: `POST /v1beta/auth_tokens`. Defaults: 30 minutes to send, 60 seconds to start.
  Preview. `v1beta` only.
- Official SDKs have Live. The .NET SDK has it too (`GoogleGenAIRealtimeClient`). Competitor
  `Google_GenerativeAI` also has it.

### 5.8 Media models

[Google doc]

- **Omni video** `gemini-omni-1.2-flash` (GA 5 Nov). Interactions API only. `response_format` of type
  `video`. Duration 3 to 15 seconds (default 10). Resolutions 360p, 720p (default), 1080p, 4K (the
  last two are upscaled). Edit, extend (to 30 seconds), interpolate and reference-image modes. Limits
  8 RPM and 40 RPD on Tier 1. Not available on the free tier.
- **Nano Banana 2.1** `gemini-nano-banana-2.1` (GA 6 Oct). Up to 14 reference images. Ratios 1:4,
  4:1, 1:8, 8:1. Sizes 1K, 2K, 4K. `thinking_level` `minimal`, `medium` (default), `high`.
  No context caching or function calling.
- **TTS** `gemini-3.8-flash-tts` and `gemini-3.8-flash-lite-tts` (GA 22 Sep). Unary output is WAV.
  Streaming output is raw `audio/l16`. At most 2 speakers. 30 prebuilt voices plus a library of 150
  or more.
- **Voices API** (`/v1beta/voices`): design, replicate (needs consent audio, Tier 1 or higher) and
  **remix** (new, 4 Nov). 200 stored voices per project. Stored voices expire 1 year after last use.
- **Lyria** `lyria-3.5` (GA 3 Sep), `lyria-3-clip-preview` and `lyria-3-pro-preview` on Interactions.
  44.1 kHz stereo MP3 or WAV. **Lyria RealTime** `lyria-realtime-exp` is a WebSocket stream with
  weighted prompts and BPM, scale and density controls.
- **Transcribe** `gemini-3.5-transcribe` (GA 26 Aug). Up to 1 hour. With diarization or word
  timestamps the limit is 30 minutes. Longer audio is truncated and still reports `completed`. Custom
  vocabulary up to 1,000 terms. It cannot combine with diarization or timestamps.
- **Embeddings** `gemini-embedding-2` (GA 22 Apr). It rejects `task_type`. Put the task in the text.
  Limits: 8,192 tokens, 6 images, 180 seconds of audio, 120 seconds of video, one PDF of 6 pages.
  `gemini-embedding-2-preview` routes to the GA model since 6 Nov. A batch path exists
  (`asyncBatchEmbedContent`).
- **Robotics ER 2** `gemini-robotics-er-2-preview` and a streaming variant. Preview.

### 5.9 Tools

[Google doc]

- **File Search:** `fileSearchStores` (create, import, upload, documents). Chunking and metadata
  filters. Multimodal search since 5 May. 100 MB per document. Store limits: 1 GB free, 10 GB Tier 1.
  It works on `generateContent` (`fileSearch` tool) and Interactions.
- **Maps grounding:** the `google_maps` tool takes optional latitude and longitude. English only.
  Citations arrive as `place_citation` annotations. A widget token exists.
- **Computer Use:** GA on 30 Oct. Environments `browser`, `mobile`, `desktop`. Optional prompt
  injection detection. Run it in a sandbox.
- **MCP:** a remote `mcp_server` tool. Streamable HTTP only. In `generateContent` it is `mcpServers`.
- **Tool combination:** Preview since 18 Mar. Built-in tools plus custom functions in one call. Use
  `validated` mode.
- **Function responses** accept audio, video and Office files since 5 Nov.
- **Office and OpenDocument input** (PPT, ODP, ODS, DOC) since 5 Nov.

### 5.10 Webhooks (4 May)

[Google doc] Preview. `/v1beta/webhooks`.

- Events: `batch.succeeded`, `batch.failed`, `batch.expired`, `interaction.requires_action`,
  `interaction.completed`, `interaction.failed`, `video.generated`. The guide also lists
  `batch.cancelled` and `interaction.cancelled`. The reference does not.
- Payload: a thin envelope `{type, version, timestamp, data:{id, ...}}`.
- Two signing schemes. Static webhooks follow the Standard Webhooks spec (`webhook-id`,
  `webhook-timestamp`, `webhook-signature`, HMAC-SHA256, `whsec_` prefix). Dynamic webhooks send an
  RS256 JWT in `Webhook-Signature`, verified against a public JWKS URL.
- Delivery is at-least-once. Reject timestamps older than 5 minutes. Retries last 24 hours.
- Secret rotation: `:rotateSigningSecret` with a 24-hour grace option.

### 5.11 Batch API changes

[Google doc]

- New: `priority` field, `updateGenerateContentBatch`, `asyncBatchEmbedContent`, `serviceTier`,
  `labels`, `store`, webhook notifications. Limits: 100 concurrent batches, 2 GB input file, 20 GB
  storage, a per-model "enqueued tokens" cap per tier. Inline requests must total under 20 MB.
- The guide says expiry is 48 hours. The webhook page says 24. They conflict.
- The guide uses `JOB_STATE_*`. The reference uses `BATCH_STATE_*`. Our live test already proved the
  wire format is `BATCH_STATE_*`.

### 5.12 Account, billing, errors and keys

[Google doc]

- **Prepay and Postpay** since 23 Mar. New users start on Prepay (minimum $5, credits expire in 12
  months). A $0 balance stops all keys with **HTTP 402** `payment_required`.
- **Tier caps:** Tier 1 $250, Tier 2 $2,000, Tier 3 $20,000 to $100,000+ per month, set per billing
  account. Reaching a cap pauses all projects until the 1st. The docs do not name the error.
- **Project spend caps** are experimental. Billing data lags about 10 minutes.
- **Spend per 10 minutes:** Tier 1 $10, Tier 2 and 3 $200. Hitting it returns 429.
- **Tier rules:** Tier 2 needs $100 paid and 3 days. Tier 3 needs $1,000 and 30 days.
- **Gemini API is excluded from the $300 Google Cloud free trial** (March 2026).
- **Generate Content storage:** `store=true` is now the default for new paid-tier projects (26 Oct).
  Opt out per request with `store=false`.
- **API keys:** new AI Studio keys are "auth keys" since 28 May. Unrestricted standard keys are
  rejected. Restricted standard keys still work. The key goes in `x-goog-api-key`.
- **Error codes** (snake_case strings): `invalid_request` (400), `failed_precondition` (400),
  `out_of_range` (416), `parameter_unknown` (400), `authentication` (401), `payment_required` (402),
  `permission_denied` (403), `not_found` and `model_not_found` (404), `already_exists` and `aborted`
  (409), `rate_limit_exceeded`, `quota_exceeded` and `too_many_requests` (429), `cancelled` (499),
  `api_error` (500), `unimplemented` (501), `service_unavailable` (503), `deadline_exceeded` (504).
  Blocked-content codes include `safety`, `recitation`, `image_recitation` and `content_blocked`.
  Generation error codes include `malformed_function_call` and `missing_thought_signature`.
  `FREE_TIER_VERIFICATION_REQUIRED` is a separate 403 case.
- I did not confirm whether `generateContent` errors use these codes or the older `RESOURCE_EXHAUSTED`
  style.
- **Partner rule:** Google asks partners to send `x-goog-api-client: company-product/version`.

### 5.13 Platform and tooling

- Official SDKs default to API `v1` since the November majors (Python 3.0.0, .NET 2.0.0). `v1beta`
  is still needed for TTS config, transcription config, Live, ephemeral tokens, agents, webhooks and
  explicit caching.
- New `gemini-api` CLI (beta, 27 Oct) built on Interactions.
- Gemini CLI is being replaced by Antigravity CLI. Individual access ended on 18 Jun.
  `google-gemini/gemini-cli` is still active (v0.68.0).
- Vertex AI is now "Gemini Enterprise Agent Platform". The Interactions client takes `enterprise=True`.
  Preview supports only the `global` location.
- Logs and datasets: AI Studio logs now cover Interactions (6 Jul).
- Response field `modelStatus` carries `modelStage`, `retirementTime` and `message`. It tells a
  caller when the model it used will retire.

---

## 6. What is old, deprecated or shut down

From Google's deprecations table (93 rows) [Google doc]. "None" means "No shutdown date announced".

| Item | State |
|---|---|
| `gemini-2.0-flash` family, `gemini-1.5-*`, `gemini-1.0-pro`, `gemini-pro` | Shut down. The library still ships the 1.x constants without `[Obsolete]`. |
| `gemini-2.5-pro`, `gemini-2.5-flash` | Deprecated. Route on 1 Dec 2026. |
| `gemini-2.5-flash-lite` | Not deprecated. |
| `gemini-2.5-flash-image` | Shuts down 15 Mar 2027. Use `gemini-3.1-flash-lite-image`. |
| `gemini-2.5-computer-use-preview-10-2025` | Shut down 28 Jul 2026. |
| `gemini-3-pro-preview` | Shut down 9 Mar 2026. |
| `gemini-3.1-flash-lite-preview` | Shut down 25 May 2026. |
| `gemini-3.7-flash`, `gemini-3.5-flash` | Deprecated 8 Oct. Routed. No shutdown. |
| `gemini-3.1-flash-lite` | Shuts down 7 May 2027. |
| `gemini-3.1-pro-preview`, `gemini-3-flash-preview` | None. |
| `gemini-3.1-flash-image` | Deprecated 6 Oct. None. Use `gemini-nano-banana-2.1`. |
| `gemini-3.1-flash-image-preview`, `gemini-3-pro-image-preview` | Shut down 25 Jun 2026. |
| `gemini-3.1-flash-tts-preview`, `gemini-2.5-*-preview-tts` | Shut down 18 Dec 2026. |
| `gemini-3.1-flash-live-preview` | None. Replacement `gemini-3.8-live`. |
| `gemini-2.5-flash-native-audio-preview-12-2025` | Shuts down 31 Mar 2027. |
| `gemini-live-2.5-flash-preview`, `gemini-2.0-flash-live-001` | Shut down 9 Dec 2025. |
| `text-embedding-004` | Shut down 14 Jan 2026. |
| `gemini-embedding-2-preview` | Deprecated. Routed to `gemini-embedding-2` since 6 Nov. |
| `gemini-embedding-001` | Shuts down 14 May 2028. |
| Imagen 4 models | Shut down 17 Aug 2026. |
| Veo 2.0 and 3.0 | Shut down 30 Jun 2026. Veo 3.1 previews shut down 22 Oct 2026. |
| `gemini-omni-flash-preview` | Shut down 22 Oct 2026. |
| `gemini-omni-1.1-flash` | Shuts down 3 Dec 2026. |
| `gemini-robotics-er-1.5-preview` | Shut down 30 Apr 2026. |
| `gemini-robotics-er-1.6-preview` | Shut down 31 Aug 2026. |
| `deep-research-pro-preview-12-2025` | Shut down 23 Oct 2026. |
| `antigravity-preview-05-2026` | Replaced 5 Oct 2026. |
| Interactions legacy `outputs` schema | Removed 8 Jun 2026. |
| GMP Contextual View tool | Shut down 15 Jun 2026. |
| Python `google-generativeai`, JS `@google/generative-ai` and similar | Deprecated 30 Nov 2025. |
| Gemini CLI for individual accounts | Ended 18 Jun 2026. |
| `temperature`, `top_p`, `top_k` | Deprecated from 3.6 Flash on. Ignored. Google says future models return an error. |
| Prefilled final model turn | HTTP 400 on 3.6 and later. |
| `thinking_budget` | Replaced by `thinking_level` on Gemini 3.8 and 4. |

Model lifecycle notes [Google doc]:

- Google gives at least 2 weeks notice before a `latest` alias moves.
- Shutdown dates in the table are the earliest possible dates.
- Google gates new keys off the 2.5 models since 18 Sep.
- Archived by Google on GitHub: the old Python, Swift and Dart SDKs, and several demo repos.

A condensed history from 2025 to May 2026 [Google doc]: Interactions API and Deep Research launched on
11 Dec 2025. Gemini 3 Pro launched 18 Nov 2025 and Gemini 3 Flash on 17 Dec. File Search launched 6
Nov 2025. `gemini-3.1-pro-preview` shipped 19 Feb 2026. Nano Banana 2 shipped 26 Feb. Lyria 3 shipped
25 Mar. Flex and Priority shipped 1 Apr. Gemma 4 shipped 2 Apr. Gemini Embedding 2 went GA on 22 Apr.
Managed agents and `gemini-3.5-flash` launched 19 May. Webhooks launched 4 May. The mid-year
`temperature` deprecation came with `gemini-3.6-flash` on 21 Jul.

---

## 7. SDK and competitor landscape

NuGet and GitHub data from 15 Nov [Repo].

| Library | Version | Last release | Downloads | Notes |
|---|---|---|---|---|
| `Google.GenAI` (official) | 2.1.0 | 10 Nov | 4.38M | Weekly releases. Targets net8.0 and netstandard2.0. Has `AsIChatClient`, image generator, Live, Batches, Caches, Tunings, Tokens, continuation helper, Native AOT notes, Vertex support. Depends on M.E.AI 10.6.0. |
| `Google_GenerativeAI` (gunpal5) | 3.6.8 | 14 Oct | 1.25M | Slow. Last features in April. Has Live and claims AOT. |
| `Mscc.GenerativeAI` | 3.1.0 | not found | 1.11M | I could not find its GitHub repo. |
| `GeminiDotnet` | 0.29.0 | 5 Nov | 244k | Regenerates clients from Google's discovery doc and tracks API revisions every few days. Markets AOT. |
| `Junaid.GoogleGemini.Net` | 6.8.0 | 13 Oct | 9,279 | 11 stars. |

Official .NET SDK gaps, from its open issues [Google repo]:

- Issue 159: Interactions API support, open since Dec 2025 with 13 comments. The source tree has
  `InteractionStatus.cs` and `InteractionsTest.cs` but no client class that I could find.
- Issue 553: Decisions API.
- Issue 461: `thinking_summaries`.
- Issue 379: `ISpeechToTextClient`.
- Issue 176: File Search methods. Types exist.

Microsoft side [Repo]:

- `Microsoft.Extensions.AI.Abstractions` is at 11.0.0 (shipped with .NET 11 on 11 Nov). The
  companion package pins 10.6.0. So does the official SDK.
- M.E.AI 10.9 added routing and failover chat clients (experimental). [Third party]
- Semantic Kernel `dotnet-1.82.0` shipped on 7 Nov.

What this means for positioning. The README claims the official SDK lacks resilience and an
`IChatClient` adapter. Both now exist there. Our library still leads on cost governance, rate
limiting, OpenTelemetry cost metrics, TTS and speech wrappers, Voices, Batch and typed accessors. Run
the peer survey again before the README keeps those claims.

---

## 8. The library today

### 8.1 Coverage of Google's 76 documented methods

[Google doc] [Repo]

Covered: `generateContent`, `streamGenerateContent`, `countTokens`, models (2), files (4 of 5),
cached contents (5), batches (5 of 8), embeddings (2 of 3), voices (4), and interactions create, get
and stream.

Not covered, about 47 methods: Decisions (1), auth tokens (1), agents (4), environments (6),
credentials (5), triggers (7), webhooks (7), File Search stores and documents (11), `files:register`,
`asyncBatchEmbedContent`, two batch updates, and interaction cancel and delete. This is my count
from the method list and a grep. Treat it as approximate.

### 8.2 Silent field drops (drift audit)

I compared Google's discovery document (revision 20261113) with the library's model properties by
name. The match is global, so a name that exists anywhere in the library counts as found. The real
number of gaps is likely higher than this.

| Schema | Fields not found in the library |
|---|---|
| Request | `labels`, `continuationToken` (and `serviceTier`, `store` are not on the generateContent request) |
| Response | `modelStatus`, `responseId` |
| UsageMetadata | `cacheTokensDetails` |
| Candidate | `avgLogprobs`, `logprobsResult`, `finishMessage`, `citationMetadata`, `groundingAttributions`, `continuationToken` |
| GenerationConfig | `audioTranscriptionConfig`, `translationConfig`, `responseLogprobs`, `logprobs`, `responseJsonSchema`, `enableEnhancedCivicAnswers`, `enableAffectiveDialog` |
| Part | `videoMetadata`, `toolCall`, `toolResponse`, `partMetadata`, `audioTranscription` |
| Tool | `googleMaps`, `fileSearch`, `computerUse`, `googleSearchRetrieval`, `mcpServers` |
| ToolConfig | `includeServerSideToolInvocations`, `retrievalConfig` |
| GroundingMetadata | `groundingSupports`, `googleMapsWidgetContextToken`, `retrievalMetadata`, `imageSearchQueries` |
| SafetyRating | `blocked` |
| FunctionResponse | `scheduling`, `willContinue` |
| CitationMetadata | `citationSources` |
| File | `source`, `sha256Hash`, `downloadUri`, `videoMetadata` |
| Model | `baseModelId`, `maxTemperature`, `thinking` |
| EmbedContentRequest | `embedContentConfig` |
| ContentEmbedding | `shape` |
| ModelStatus | `modelStage`, `retirementTime` |

`groundingSupports` matters most. It maps each answer span to its sources, and a UI needs it to show
citations. `finishReason` is a plain string in the library, so the new values (`CONTINUATION`,
`MALFORMED_RESPONSE`, `ESCALATION`) do not break it.

### 8.3 Risks, most urgent first

1. **Timeout cap.** `GeminiOptionsValidator` rejects `TimeoutSeconds` above 300
   (`GeminiOptionsValidator.cs:77`). Google says allow 10 to 20 minutes for Flex and long decode.
   Default is 100 seconds. A Gemini 4 call that thinks for more than 100 seconds fails.
2. **No Gemini 4 support.** No constants, no prices, no `xhigh`, no continuation. A call that sets
   `MaxOutputTokens` above 32,768 non-streaming on Carbon or Argon gets HTTP 400. Explicit caching
   fails on all three. The governor under-counts spend for any unlisted model.
3. **1 December routing.** `Gemini25Pro` and `Gemini25Flash` are not `[Obsolete]`. From 1 Dec Google
   routes them and bills at 3.8 Flash and 3.5 Flash-Lite rates. The governor prices them as 2.5. The
   same class of bug as the 3.5 and 3.7 routing that the library already handles.
4. **Wrong shutdown dates.** The `[Obsolete]` messages on the three old TTS constants say 17 Nov. The
   real date is 18 Dec. The 6.8.0 entry in `ROADMAP.md` repeats the old date. Check
   `PLAN-tts-3-8.md` too. I did not read it.
5. **Request fields missing.** No `ServiceTier`, `Store`, `Labels` or `ContinuationToken` on the
   generateContent request. `store` is now on by default for new paid projects, so a caller cannot
   opt out per request through the library.
6. **Flex and Priority are not priced.** When the field exists, the governor must price by
   `usageMetadata.serviceTier`, because Priority can downgrade to standard.
7. **No `x-goog-api-client` header.** Google asks partners to send it. The library sends only
   `User-Agent: Junaid.GoogleGemini.Net`, with no version.
8. **Error mapping unverified.** `payment_required` (402), `out_of_range` (416), `cancelled` (499),
   `unimplemented` (501) and the three 429 codes may not map to typed exceptions.
9. **Issue 61** (live-test failure) is open since 5 Oct. The CI logs have expired. Runs on 19 Oct, 2
   Nov and 9 Nov passed.
10. **No `net10.0` or `net11.0` target.** The companion pins M.E.AI 10.6.0. From my own background
    knowledge, .NET 8 and 9 reach end of support in November 2026. I did not verify this.
11. **Stale constants.** `Gemini15*`, `GeminiPro` and `Gemini10Pro` point at shut-down models.
12. **README claims** about the official SDK are partly stale (section 7).
13. **Old date in docs.** `docs/articles/tts.md` and the XML docs may repeat the 17 Nov date.

Removed from the first pass: the image default shutdown risk, and the `Api-Revision` risk (both
low or false now).

---

## 9. What should be next

### Step 1: patch 6.8.1 (this week)

Goal: stop silent failures. Small and safe.

Status: built on the branch `release/6.8.1` as version 6.8.1, and not yet merged or released. Items 1
to 4 and 6 are done. Item 5 (close issue 61) is not done, because closing an issue is a visible
action on GitHub. Two things were found while building it, and are also in the release:

- The 300-second limit was enforced twice, by a `[Range]` attribute and by the validator. Both
  changed.
- `GeminiTelemetry` reported version 6.4.1 for seven releases. It now reads the assembly version.

1. Raise the `TimeoutSeconds` cap (for example to 3,600). Update the validator message and docs.
2. Fix the TTS `[Obsolete]` dates (18 Dec). Mark `Gemini25Pro` and `Gemini25Flash` obsolete with the
   1 Dec routing note. Mark `Gemini15*`, `GeminiPro`, `Gemini10Pro` obsolete.
3. Add price routing for `gemini-2.5-pro` and `gemini-2.5-flash` that starts on 1 Dec. Use the
   date-aware mechanism from 6.8.0.
4. Send `x-goog-api-client: junaid-googlegemini-net/<version>` and add the version to `User-Agent`.
5. Close issue 61.
6. Add the keyless "Google drift watch" workflow (section 9, step 5).

### Step 2: release 6.9.0, Gemini 4 on `generateContent`

Goal: make the new models work and keep cost tracking honest. Size: medium.

1. Constants for the three models. Add `ThinkingLevel.XHigh`.
2. Prices with the 1 Dec change for Carbon and Argon. Add Hydrogen, Flex, Priority and Batch rates.
   Use the table in section 5.3 and re-check it against the live page first.
3. Request fields: `ServiceTier`, `Store`, `Labels`, `ContinuationToken`. Response fields:
   `ContinuationToken` on the candidate, `UsageMetadata.ServiceTier`, `CacheTokensDetails`,
   `ModelStatus`, `ResponseId`.
4. A continuation loop: resend the same request, keep `maxOutputTokens`, join the text, and add up
   usage across hops. The governor must record every hop. Cancel and resume must not double count.
5. Guards with clear messages: non-streaming `MaxOutputTokens` above 32,768 on Carbon and Argon,
   `CachedContent` on Gemini 4, `minimal` on Gemini 4 and 3.8 Flash, `xhigh` off Hydrogen, and
   `thinkingBudget` combined with `thinkingLevel`.
6. A logged warning from `modelStatus.retirementTime`. This is a feature no other .NET client offers.
7. Add the missing response fields from section 8.2, starting with `groundingSupports`,
   `finishMessage` and `citationMetadata`.
8. Decide whether `Recommended` moves to Hydrogen (see the decision below).

### Step 3: release 6.10.0, new APIs

1. **Decisions API.** Small, self-contained, cheap and a fit for cost governance. Use the STT plan
   pattern: plan file, live check, then code.
2. **Webhook receiver for ASP.NET Core.** Verify Standard Webhooks signatures (and the JWT scheme),
   reject old timestamps and deduplicate on `webhook-id`. Add `WebhookService` for the create, list,
   ping and rotate methods. Tie it to Batch so `WaitUntilComplete` can use a callback instead of
   polling. This fits the library's ASP.NET Core identity and I found no competitor with it.
3. **Batch updates:** `priority`, `updateGenerateContentBatch`, `asyncBatchEmbedContent`.
4. **File Search stores and documents,** `files:register`, interaction cancel and delete.

### Step 4: bigger bets (decide one at a time)

| Item | Size | Reason | Risk |
|---|---|---|---|
| General Interactions client (background, steps, tools) | Large | Agents, Omni, Lyria, Decisions-adjacent features and Google's future work land here. | Schema churn. |
| Live API package `Junaid.GoogleGemini.Net.Live` | Large | Official SDK has it. It is the last open ROADMAP item. | Protocol size, preview status, no cheap CI test. |
| Omni video `GenerateVideoAsync` | Medium | Veo is gone. Omni is the only video path. Needs per-second pricing in the governor. | Price model differs. |
| Managed agents, environments, credentials, triggers | Medium to large | Unique long-running flows. | Preview. Names change every few months. |
| Vertex / Enterprise Agent Platform backend | Large | Biggest structural gap. Reaches regulated and EU buyers. | Different auth. |
| `net10.0` and `net11.0` targets, M.E.AI 11 | Small | Keeps the companion current. | Low. |
| Native AOT and trim annotations | Medium | Two competitors market it, and the official SDK documents it. | Reflection in `JsonSchemaGenerator`. |
| Lyria music, `gemma-4` | Small each | Parity. | Low demand. |

### Step 5: the keyless "Google drift watch" (high value, low cost)

Add a weekly workflow that needs no API key. It would:

1. Download `https://generativelanguage.googleapis.com/$discovery/rest?version=v1beta`. Compare
   `revision` and the schema property names with a stored snapshot. Open an issue when new fields
   appear. This is how `GeminiDotnet` stays current.
2. Download `https://aistudio.google.com/docs/deprecations.md`. Diff the table. Open an issue when a
   shutdown date changes or a model we ship appears.
3. Download `https://aistudio.google.com/docs/changelog.md`. Report the newest entry date.
4. Optionally call `GET /models/{id}` (free, needs a key) for every constant in `GeminiConstants`.

This would have caught the Gemini 4 launch, the TTS date change and the 2.5 routing within a week.

### Decision for you: the default model

Do not move `Recommended` to Hydrogen yet without a plan. Hydrogen costs $1.50 and $7.50 today.
`gemini-3.8-flash` costs $0.75 and $3.75 until 31 Dec, then the same $1.50 and $7.50. Hydrogen also
thinks at `high` by default. The same call can cost more than twice as much. Options: keep 3.8
Flash until 1 Jan, or move now and set the default thinking level to `medium`.

### Do not do now

- Do not build Veo support. Google shut it down.
- Do not wrap Gemini CLI or Antigravity CLI. They are end-user tools.
- Do not make Argon the default. It is the same price as Carbon after 1 Dec.
- Do not add a `net48`-only feature. Keep the netstandard2.0 build working.

---

## 10. Conflicts inside Google's own docs

Check these before you rely on any one of them.

1. **`gemini-3.1-flash-image` shutdown:** the deprecations table and the changelog say no date. A
   copy in the `gemini-skills` repo says 29 Oct. I used the table.
2. **Old TTS shutdown:** the table says 18 Dec. Third-party summaries said 17 Nov.
3. **Batch state names:** `JOB_STATE_*` in the guide, `BATCH_STATE_*` in the reference. The library
   proved `BATCH_STATE_*` live.
4. **Batch expiry:** 48 hours in the guide, 24 hours in the webhook catalog.
5. **Non-streaming time limit** on long decode: 10 minutes in one section, 20 in another.
6. **Async function calling default** on Live: the model page says `NON_BLOCKING` is the default. The
   tools page says calls run sequentially by default.
7. **Live scheduling value:** `INTERRUPT` on one page, `INTERRUPTED` on another.
8. **Live languages:** 70 on the overview, a list of 99 codes elsewhere.
9. **Ephemeral token field:** `liveConnectConstraints` in the guide, `bidiGenerateContentSetup` in the
   reference.
10. **Computer Use coordinates:** pixels in one place, a 0 to 999 grid in another. The REST sample
    uses `/v1`, others use `/v1beta`.
11. **Prepay to Postpay:** the FAQ says no. Another section says yes.
12. **Video tokens:** 263 per second in one bullet, about 100 or 300 per second in a table.
13. **`responseJsonSchema`** has a self-referential description. `responseSchema` is absent from the
    `generateContent` reference.
14. **Interactions status:** the status table omits `queued`, which the samples poll for.
15. **Explicit caching error text** for Gemini 4 and the `minimal` error text are not documented.

---

## 11. Still unverified

1. Live API checks: `GET /v1beta/models/gemini-4-*` and a real Gemini 4 call, to confirm limits,
   the 400 on non-streaming above 32,768, and the explicit caching error text.
2. Whether `generateContent` errors use the new snake_case codes.
3. The exact Argon and Carbon `thinkingBudget` range.
4. The 12 Jan 2027 cache-control change and whether it reaches the Gemini API.
5. Whether the official .NET SDK has an Interactions client.
6. The `Mscc.GenerativeAI` repo and its current features.
7. The pages I did not fetch: `veo.md`, `logs-datasets.md`, `safety-settings.md`, `url-context.md`,
   `google-search.md`, `code-execution.md`, `media-resolution.md`, `text-generation.md`, the per-model
   pages for Hydrogen, Gemma 4 and Lyria, and `llms-full.txt`.
8. The tails of four long pages that the fetch agents could only summarize: `migrate-to-interactions`,
   `file-search`, `computer-use`, `agent-environment`.

---

## Sources

Google docs (all read on 15 Nov 2026 as markdown at `aistudio.google.com`):

- [llms.txt index](https://aistudio.google.com/llms.txt)
- [Release notes](https://aistudio.google.com/docs/changelog.md)
- [Deprecations](https://aistudio.google.com/docs/deprecations.md)
- [Pricing](https://aistudio.google.com/docs/pricing.md)
- [Rate limits](https://aistudio.google.com/docs/rate-limits.md)
- [Billing](https://aistudio.google.com/docs/billing.md)
- [API errors](https://aistudio.google.com/docs/api-errors.md)
- [API keys](https://aistudio.google.com/docs/api-key.md)
- [API versions](https://aistudio.google.com/docs/api-versions.md)
- [Latest models](https://aistudio.google.com/docs/latest-model.md)
- [Gemini 4 Argon](https://aistudio.google.com/docs/whats-new-gemini-4-argon.md)
- [Gemini 4 Carbon](https://aistudio.google.com/docs/whats-new-gemini-4-carbon.md)
- [Gemini 4 Hydrogen](https://aistudio.google.com/docs/whats-new-gemini-4-hydrogen.md)
- [Gemini 3.6 and 3.5 Flash-Lite](https://aistudio.google.com/docs/whats-new-gemini-3.6.md)
- [Gemini 3.8 Flash](https://aistudio.google.com/docs/whats-new-gemini-3.8.md)
- [Long decode](https://aistudio.google.com/docs/long-decode.md)
- [Background execution](https://aistudio.google.com/docs/background-execution.md)
- [Thinking](https://aistudio.google.com/docs/thinking.md)
- [Caching](https://aistudio.google.com/docs/caching.md)
- [Interactions overview](https://aistudio.google.com/docs/interactions-overview.md)
- [Interactions breaking changes (May 2026)](https://aistudio.google.com/docs/interactions-breaking-changes-may-2026.md)
- [Migrate to Interactions](https://aistudio.google.com/docs/migrate-to-interactions.md)
- [Flex inference](https://aistudio.google.com/docs/flex-inference.md)
- [Priority inference](https://aistudio.google.com/docs/priority-inference.md)
- [Decisions API](https://aistudio.google.com/docs/decisions-api.md)
- [Batch API](https://aistudio.google.com/docs/batch-api.md)
- [Webhooks](https://aistudio.google.com/docs/webhooks.md)
- [Live API](https://aistudio.google.com/docs/live-api.md)
- [Gemini 3.8 Live](https://aistudio.google.com/docs/models/gemini-3.8-live.md)
- [Managed agents](https://aistudio.google.com/docs/agents.md)
- [Antigravity agent](https://aistudio.google.com/docs/antigravity-agent.md)
- [Deep Research](https://aistudio.google.com/docs/deep-research.md)
- [Omni video](https://aistudio.google.com/docs/omni.md)
- [Image generation](https://aistudio.google.com/docs/image-generation.md)
- [Speech generation](https://aistudio.google.com/docs/speech-generation.md)
- [Embeddings](https://aistudio.google.com/docs/embeddings.md)
- [File Search](https://aistudio.google.com/docs/file-search.md)
- [Libraries](https://aistudio.google.com/docs/libraries.md)
- [API reference: all methods](https://aistudio.google.com/api-reference/all-methods.md)
- [API reference: generateContent](https://aistudio.google.com/api-reference/generate-content/generate-content.md)
- [Discovery document](https://generativelanguage.googleapis.com/$discovery/rest?version=v1beta)

Google repos:

- [gemini-skills](https://github.com/google-gemini/gemini-skills)
- [dotnet-genai releases](https://github.com/googleapis/dotnet-genai/releases)
- [dotnet-genai CHANGELOG](https://github.com/googleapis/dotnet-genai/blob/main/CHANGELOG.md)
- [python-genai CHANGELOG](https://github.com/googleapis/python-genai/blob/main/CHANGELOG.md)
- [dotnet/extensions v11.0.0](https://github.com/dotnet/extensions/releases/tag/v11.0.0)

Competitors and third parties:

- [GeminiDotnet releases](https://github.com/rabuckley/GeminiDotnet/releases)
- [Google_GenerativeAI releases](https://github.com/gunpal5/Google_GenerativeAI/releases)
- [Forum: Gemini 4 removes explicit context caching](https://discuss.ai.google.dev/t/title-gemini-4-removes-explicit-context-caching-my-per-call-cost-goes-up-4x-with-no-way-to-control-it/200723)
- [Gemini CLI to Antigravity CLI notice](https://github.com/google-gemini/gemini-cli/discussions/27274)
