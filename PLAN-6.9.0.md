# Plan: release 6.9.0 (Gemini 4 and the storage opt-out)

Goal: correct cost budgets for Gemini 4 before Carbon and Argon double in price at 00:00 UTC on
2026-12-01, and let callers keep requests out of Google's logs. The PR should be ready for review by
2026-11-25, so users have days to upgrade.

The facts behind every value here are in `PLAN-gemini-4.md`, sections 3 and 4. They were checked on
2026-11-16 and 2026-11-18 against Google's raw pages and live calls. That file is the source of truth
for Gemini 4 behavior. This file says what to build.

Work on the branch `feat/gemini-4`. It already holds two commits: opt-in probe tests and a `filter`
input on the live workflow. `PLAN-gemini-4.md`, `PLAN-6.9.0.md` and an edit to
`PLAN-gemini-landscape-2026-11.md` are not committed yet. Commit them first.

## Rules for every step

- **Writing style.** `CLAUDE.md` applies to every doc, XML comment, commit message, PR body and
  release note: ASD STE-100, short sentences, active voice, no em dashes. Before each commit, search the
  changed files for the em dash (U+2014) with the Grep tool and the pattern `\x{2014}`. The shell's
  `grep -P` fails on this machine's locale.
- **Scope.** Build exactly the steps below. The table "Decided: keep as is" lists what stays out and
  why. If a fact on Google's side disagrees with this plan, stop that step, record the evidence in
  `PLAN-gemini-4.md` section 4, and ask the maintainer.
- **Baseline.** Release build: 0 warnings. `TreatWarningsAsErrors` is on for the core package. Unit
  tests: 324 pass on `net9.0`, 322 on `net48`, 324 on `net8.0`, and 6 in the Testing package. Every
  number may only go up.
- **This machine.** No `GeminiApiKey` exists anywhere on it. Do not look for one. Live tests run on
  GitHub (step 6). `python3` does not exist. Use `py -3`. In Bash, `$TEMP` contains a space, so use the
  session scratchpad for temporary files. The .NET 8 runtime is missing. Run `net8.0` tests with
  `DOTNET_ROLL_FORWARD=Major`.

## Steps

### 1. Re-check the prices

Google can change a price at any time. Fetch the pricing page and confirm the numbers in step 3:

```
curl -sS -A "Mozilla/5.0" https://aistudio.google.com/docs/pricing.md
```

Read the "Standard" tab of the sections `gemini-4-hydrogen`, `gemini-4-carbon` and `gemini-4-argon`.

**Done when:** the input, output and cached rates for all three models, before and after 1 Dec,
match step 3. Otherwise stop and ask.

### 2. Model constants

In `Junaid.GoogleGemini.Net/Infrastructure/Utilities/GeminiConstants.cs`:

- In `Models`, add a Gemini 4 block above the 3.x models:
  `Gemini4Hydrogen = "gemini-4-hydrogen"`, `Gemini4Carbon = "gemini-4-carbon"`,
  `Gemini4Argon = "gemini-4-argon"`. Give each an XML summary in the style of `Gemini38Flash`: what it
  is for, free or paid tier, default thinking level, the levels it accepts, and that explicit caching
  is not supported. Take the values from `PLAN-gemini-4.md` section 3.
- Add the three to `ContentGenerationModels`.
- In `ThinkingLevels`, add `XHigh = "xhigh"`, with a summary that only `gemini-4-hydrogen` accepts it
  and that Carbon and Argon return HTTP 400. Update the class summary: `minimal` is rejected by all
  three Gemini 4 models (live, 2026-11-16).
- Leave `Defaults.Model`, `Recommended` and `Fastest` as they are.

Tests: extend `tests/Junaid.GoogleGemini.Net.Tests/ModelLifecycleTests.cs` with the three IDs, the
`ContentGenerationModels` entries and `XHigh`.

**Done when:** the build has 0 warnings and the new tests pass.

### 3. Cost governor prices

In `Junaid.GoogleGemini.Net/Infrastructure/GeminiCostGovernor.cs`:

- Add `private static readonly DateTime Gemini4FrontierPriceRiseUtc = new(2026, 12, 1, 0, 0, 0,
  DateTimeKind.Utc);` next to `Gemini25ProRoutingStartsUtc`. Its comment must give the reason for
  00:00 UTC: Google gives no time zone, the change raises the price, and the earliest plausible time
  never undercounts a budget. This is the opposite case of `Gemini25ProRoutingStartsUtc`, which lowers
  a price and uses 08:00 UTC. Say so.
- Add three `DefaultPricing` entries. Use `new ModelPricing { ... }` with `NextPricing` and
  `NextPricingEffectiveFromUtc`, as the `gemini-2.5-pro` entry does. The `Introductory(...)` helper
  is fixed to 2027-01-01, so it does not fit here.

| Model | Until 2026-11-30 23:59:59 UTC (input / output / cached) | From 2026-12-01 00:00:00 UTC |
|---|---|---|
| `gemini-4-hydrogen` | $1.50 / $7.50 / $0.075 | Same, no schedule |
| `gemini-4-carbon` | $2.00 / $10.00 / $0.10 | $4.00 / $20.00 / $0.20 |
| `gemini-4-argon` | $2.00 / $10.00 / $0.10 | $4.00 / $20.00 / $0.20 |

  None of the three has a high-volume tier. Google prices the full 1M context at one rate.
- Update the date in the `DefaultPricing` XML summary ("re-verified ... on") to the date of step 1.

Tests: in `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/GeminiCostGovernorScheduleTests.cs`,
copy the pattern of the `Gemini25Pro_...` tests. Cover, for Carbon and for Argon, the last second
before and the first second of the rise. With one million input and one million output tokens, that
is $12.00 before and $24.00 after. Cover Hydrogen at $9.00 on dates before, at and after the rise.
Add one case with `CachedContentTokenCount`, so the cached rate is covered on both sides of the rise.

**Done when:** the new tests pass, and each fails if you change the matching rate or the switch time.

### 4. The `Store` option

Since 2026-10-26, Google stores `generateContent` requests by default for new paid projects. The
opt-out is `store: false` on each request (`PLAN-gemini-4.md`, section 5). Add it without changing
any default.

- `Junaid.GoogleGemini.Net/Models/GoogleApi/GenerateContentRequest.cs`: add
  `[JsonPropertyName("store")] public bool? Store { get; set; }`. Null values are not serialized, so an
  unset value sends nothing.
- `Junaid.GoogleGemini.Net/Models/Requests/GeminiRequestOptions.cs`: add `public bool? Store { get;
  set; }`. Explain in its summary: `null` keeps the project setting, `false` opts this request out,
  `true` opts it in. Add it to `Clone()`.
- `Junaid.GoogleGemini.Net/Infrastructure/Factories/RequestFactory.cs`: four methods build a
  `GenerateContentRequest`, and each copies `CachedContent = options?.CachedContent`. Add
  `Store = options?.Store` next to each of the four.

Tests, with `FakeHttpMessageHandler` and the request body capture that `CachingServiceTests` uses:

- `Store = false` puts `"store":false` in the body. `Store = null` puts no `store` key in the body.
- One test for each of the four `RequestFactory` methods. Find the public `IGeminiService` method that
  reaches each one, and send the request through it.
- `Clone()` copies `Store`.

**Done when:** the tests pass, and deleting any one of the five new copy lines (four sites and
`Clone`) makes at least one test fail. Check this by removing each line in turn, as 6.8.1 did for the
timeout limit.

### 5. MIME types

In `Junaid.GoogleGemini.Net/Infrastructure/Utilities/FileUtilities.cs`, add these to
`MimeTypeMappings`:

| Extension | MIME type |
|---|---|
| `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| `.doc` | `application/msword` |
| `.dotx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.template` |
| `.odt` | `application/vnd.oasis.opendocument.text` |
| `.pptx` | `application/vnd.openxmlformats-officedocument.presentationml.presentation` |
| `.ppt` | `application/vnd.ms-powerpoint` |
| `.odp` | `application/vnd.oasis.opendocument.presentation` |
| `.xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| `.xls` | `application/vnd.ms-excel` |
| `.ods` | `application/vnd.oasis.opendocument.spreadsheet` |
| `.aiff` | `audio/aiff` |
| `.ogg` | `audio/ogg` |
| `.mpeg` | `video/mpeg` |
| `.mpg` | `video/mpg` |
| `.flv` | `video/x-flv` |
| `.wmv` | `video/wmv` |
| `.3gp` | `video/3gpp` |

These strings come from Google's document, audio and video pages. Remove the "(for future multimodal
support)" comments, since the library already sends audio and video.

Tests: one theory with every row above, plus a row each for `.avi` and `.mp3` that pins their current
values (`video/x-msvideo`, `audio/mpeg`).

**Done when:** the theory passes.

### 6. Live tests

Create `tests/Junaid.GoogleGemini.Net.IntegrationTests/Gemini4BehaviorLiveTests.cs`, modeled on
`Gemini38BehaviorLiveTests.cs`. Use the new constants. Use `[RequiresGeminiKey]` for Hydrogen-only
tests (Hydrogen has a free tier) and `[RequiresPaidGeminiKey]` for any test that calls Carbon or Argon.

1. Each of the three models answers "Reply with the single word OK." through `IGeminiService`, with
   `ThinkingLevel = Low`.
2. `XHigh` on Hydrogen succeeds. `XHigh` on Carbon throws `GeminiApiException` with
   `HttpStatusCode.BadRequest`, and the message contains `XHIGH`.
3. `Minimal` on Hydrogen throws `GeminiApiException`, `BadRequest`, and the message contains `MINIMAL`.
4. Creating explicit cached content for Hydrogen through `ICachingService` throws
   `GeminiApiException`, `BadRequest`, and the message contains `Explicit caching is not supported`.
5. `Store = false` on Hydrogen succeeds and returns text.
6. The governor prices a real call correctly, on Hydrogen and on Carbon. Build the service with a
   budget enabled, as `CostGovernanceLiveTests` does. Make one call. Compute the expected cost from the
   response's `UsageMetadata` and the rates in step 3 for the current UTC date. Count thought tokens as
   output and cached tokens at the cached rate. Assert that the governor's recorded spend equals it.

In `Gemini4ProbeLiveTests.cs`, delete `Probe1` to `Probe4`, because the tests above replace them. Keep
`Probe5_Continuation_OnCarbon`, its helpers and `RequiresProbeOptInAttribute`. 6.11.0 needs them.

Push the branch, then run the live tests on GitHub. Start one run at a time. All runs share one key,
and the TTS models allow 10 requests per minute, so two overlapping runs fail with HTTP 429.

```
gh workflow run integration.yml --ref feat/gemini-4 -f paid=true -f filter="FullyQualifiedName~Gemini4Behavior"
gh workflow run integration.yml --ref feat/gemini-4 -f full=true -f paid=true
```

Wait for the first run to finish before you start the second. Read results with
`gh run view <id> --log`.

**Done when:** the filtered run passes every test, and the full run has 0 failures. If a test fails,
find the cause from the log before you change anything. `ToolsLiveTests.UrlContext_ReturnsUrlRetrievalMetadata`
is known to be flaky. Before you call a failure unrelated, run the same workflow on `master` and
compare.

### 7. Docs

- `README.md`: next to the notes about 3.8 Flash (around the `ThinkingLevel` example), add a short
  Gemini 4 note. Cover the three constants, the levels each accepts, no explicit caching, ignored
  sampling fields, and why the default model stays `gemini-3.8-flash` (section "Decided" below).
- `docs/articles/cost-governance.md`: add the Gemini 4 rates and the 1 Dec rise, next to the
  paragraph about the 2027 introductory prices.
- `docs/articles/files-and-caching.md`: next to the note about the free-tier caching limit, say that
  Gemini 4 does not support explicit caching and uses implicit caching from 4,096 tokens. Add that
  Office and OpenDocument files can be sent directly, that `GetMimeType` now maps them, and that Gemini
  4 reads an unsliced Office document as page images (about 560 tokens a page). Say that slicing comes
  in 6.10.0.
- `docs/articles/resilience-and-rate-limiting.md`: it already mentions Gemini 4. Add one sentence:
  for hard prompts on Carbon or Argon, set `TimeoutSeconds` to 900 or more.

**Done when:** every item above is in the docs, and the Grep tool finds no `\x{2014}` in the changed files.

### 8. Drift snapshots and issue #65

Issue #65 reports the slicing fields that Google added on 2026-11-16. Slicing is planned for 6.10.0,
so 6.9.0 needs no code change for it. Google may have changed more since then, so check first:

```
py -3 .github/scripts/google_drift_watch.py --report <scratchpad>/drift.md
```

Read the report. If it shows something beyond #65 that affects this release, stop and ask. If not, run
`py -3 .github/scripts/google_drift_watch.py --update` and commit `.github/drift/`.

**Done when:** a second run without `--update` prints "No drift." and exits with 0.

### 9. Version, release notes and ROADMAP

- `Junaid.GoogleGemini.Net/Junaid.GoogleGemini.Net.csproj`: set `<Version>6.9.0</Version>`. Add a new
  `6.9.0:` entry at the top of `<PackageReleaseNotes>`. Keep every older entry. Follow the style of the
  `6.8.1:` entry: a short intro, numbered items, and a line on what is not verified. Say that there is
  no breaking change, and that `GetMimeType` now returns real types for Office files where it returned
  `application/octet-stream`.
- `ROADMAP.md`: add a `6.9.0` entry under Phase 4, after the `6.8.1` entry and in its style. Cover
  what changed and why, what was verified (with test counts and the live run IDs), and what was not.
- `PLAN-gemini-4.md`: in section 7, mark 6.9.0 as built, with the PR number once it exists.
- `Junaid.GoogleGemini.Net.Extensions.AI` and `Junaid.GoogleGemini.Net.Testing` keep their versions.
  Their code does not change.

**Done when:** the three files are updated and contain no `\x{2014}`.

### 10. Final checks and the PR

Run each command and read its output:

```
dotnet build -c Release --no-incremental
dotnet test tests/Junaid.GoogleGemini.Net.Tests/Junaid.GoogleGemini.Net.Tests.csproj -c Release -f net9.0 --no-build
dotnet test tests/Junaid.GoogleGemini.Net.Tests/Junaid.GoogleGemini.Net.Tests.csproj -c Release -f net48 --no-build
DOTNET_ROLL_FORWARD=Major dotnet test tests/Junaid.GoogleGemini.Net.Tests/Junaid.GoogleGemini.Net.Tests.csproj -c Release -f net8.0 --no-build
dotnet test tests/Junaid.GoogleGemini.Net.Testing.Tests/Junaid.GoogleGemini.Net.Testing.Tests.csproj -c Release -f net9.0 --no-build
dotnet pack Junaid.GoogleGemini.Net/Junaid.GoogleGemini.Net.csproj -c Release --no-build -o <scratchpad>/pack
```

Open the `.nupkg` and confirm the version is 6.9.0, it has `net8.0`, `net9.0` and `netstandard2.0`
with XML docs, and the release notes contain no em dash. Scan the changed files for keys
(`AIza` followed by 20 or more characters) and personal email addresses. The dummy test key
`AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345` is expected.

Then open a PR from `feat/gemini-4` to `master`. Use the `pr` skill for the body. Put `Closes #65` in
the body, so the merge closes the drift issue.

**Done when:** the build has 0 warnings, every test count is at or above the baseline, the full live
run from step 6 has 0 failures, the package checks pass, and the PR is open with green CI.

Stop there. Branch protection needs the maintainer's approving review, and that is by design. After
the maintainer merges and asks for the release, follow `RELEASE-RUNBOOK.md` from step 4.

## Decided: keep as is

| Item | Decision | Reason |
|---|---|---|
| Checks before the call (for example, `maxOutputTokens` above 32,768 on Carbon) | Leave them to Google. | Live, Google accepted values that its own docs said it rejects. Google's 400 messages are clear and exact. A check of ours can go stale or block a valid call. |
| `Defaults.Model` and `Recommended` | Stay `gemini-3.8-flash`. | Google now recommends Hydrogen, but Hydrogen thinks at `high` by default and costs twice as much per token until 1 Jan. The maintainer decides in 6.11.0, with measured costs. |
| `.avi` and `.mp3` mappings | Keep `video/x-msvideo` and `audio/mpeg`. | Google lists `video/avi`, but nobody has tested which one it accepts. A change needs a live test with a real file. |
| `Store` in the `IChatClient` adapter | Not in 6.9.0. | It would change the `Extensions.AI` package and its version. Note it in the ROADMAP entry as a follow-up. |
| Continuation loop, `ServiceTier`, `Labels`, slicing fields, transcription `Mode`, `netstandard2.0` retry changes, `net10.0` target | Later releases. | See `PLAN-gemini-4.md`, section 7. |
| Time of the Carbon and Argon rise | 00:00 UTC on 2026-12-01. | Google gives no time zone. The price rises, so the earliest plausible time keeps a budget safe. |
