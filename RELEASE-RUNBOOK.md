# Release runbook

> **Audience:** maintainer, cutting a release. Not customer-facing — see
> `Junaid.GoogleGemini.Net/RELEASE.md`'s `PackageReleaseNotes` for that.

This exists because of `v6.4.2`: its first tag push "succeeded" by every visible signal — CI was
green — while actually publishing nothing. NuGet Trusted Publishing's login step reported success
and handed back an empty API key under the wrong output name, and nothing in the process at the
time would have caught that short of reading the raw log. Step 6 below is the one that matters most.

## Steps

1. **Pre-flight — run the tests that actually catch regressions.**
   - Unit suite: `dotnet test tests/Junaid.GoogleGemini.Net.Tests/Junaid.GoogleGemini.Net.Tests.csproj -c Release`
   - Live suite, against a real key: `GeminiApiKey=<key> dotnet test tests/Junaid.GoogleGemini.Net.IntegrationTests/Junaid.GoogleGemini.Net.IntegrationTests.csproj -c Release --filter "FullyQualifiedName!~Batch"`
   - Add `GeminiPaidTier=1` (same command) if the key is billing-enabled, to also cover image
     generation and context caching. **Don't assume the key's tier from a self-skip** — a skipped
     paid-tier test only means this flag wasn't set, not that the key is free-tier. Add
     `--filter "FullyQualifiedName~Batch"` separately to cover Batch (real per-job cost, ~90s poll)
     when it's worth exercising too.
   - `dotnet build -c Release` across all three targets: 0 warnings expected
     (`TreatWarningsAsErrors` is on for the core package).

2. **Bump the version and write real release notes**, not a stub:
   - `<Version>` in `Junaid.GoogleGemini.Net/Junaid.GoogleGemini.Net.csproj`.
   - Prepend an entry to that same file's `<PackageReleaseNotes>` — this is what NuGet.org shows,
     so it's the customer-facing changelog. Keep prior entries; don't replace them.
   - Add the matching `ROADMAP.md` entry under "Phase 4" — the fuller, maintainer-facing account
     (what broke, how it was found, how it was verified). This repo's convention is to over-document
     here, not under-document.

3. **PR it and get it merged.** Branch protection requires one approving review; that's by design
   and not something this runbook — or an agent — should try to route around. `git checkout -b`,
   commit, push, `gh pr create`, then the repo owner reviews and merges.

4. **Tag and push:**
   ```
   git checkout master && git pull --ff-only origin master
   git tag -a vX.Y.Z -m "..."
   git push origin vX.Y.Z
   ```

5. **Watch CI — both jobs, not just the summary.**
   `gh run list --workflow=ci.yml --limit 2` to find the tag-triggered run, then
   `gh run view <id>` until both `Build & Test` and `Publish to NuGet` show ✓.

6. **Verify the actual push, not the checkmark.** This is the step `v6.4.2` skipped, and the one
   that would have caught the bug immediately:
   ```
   gh run view <id> --log | grep -E "Pushing|Created|Conflict|error"
   ```
   Look for the literal `Created https://www.nuget.org/api/v2/package/ ...` and
   `Your package was pushed.` lines for **the package you're releasing** (a pre-existing sibling
   package correctly shows `Conflict` + "already exists" under `--skip-duplicate` — that's success,
   not failure). A green job with no `Created` line for your package is the exact failure mode this
   step exists to catch.

7. **Poll nuget.org until the package is actually downloadable.** A successful push and a
   propagated, downloadable package are different facts — nuget.org's CDN/search index lags the
   raw upload by anywhere from under a minute to several minutes:
   ```
   until curl -s -o /dev/null -w "%{http_code}" \
     "https://api.nuget.org/v3-flatcontainer/junaid.googlegemini.net/X.Y.Z/junaid.googlegemini.net.x.y.z.nupkg" \
     | grep -q "^200$"; do sleep 15; done
   ```
   (lowercase the version in the URL). Don't declare the release done before this returns 200.

8. **Create the GitHub Release**, matching the style of prior releases (`gh release view vPREV`
   shows the house style: numbered highlights, a build/test line, a cross-package note, a
   `ROADMAP.md` link):
   ```
   gh release create vX.Y.Z --title "vX.Y.Z" --notes-file <path>
   ```
   It's marked "Latest" automatically as the newest non-prerelease release.

9. **Clean up.** Delete the merged branch, local and remote:
   ```
   git branch -d <branch> && git push origin --delete <branch>
   ```

## Things this runbook does NOT cover, on purpose

- **Bypassing branch-protection review.** Not automatable by design, and an agent asked to "just
  merge it" should refuse and say why, not look for a workaround.
- **A staging/test NuGet feed.** There isn't one; `--skip-duplicate` plus this runbook's step 6 are
  the safety net instead.
- **Rotating or storing a NuGet API key.** Trusted Publishing (OIDC, `NuGet/login@v1.2.0` in
  `ci.yml`) means there's nothing to rotate — but see the next point.
- **Detecting a break in Trusted Publishing itself before it blocks a release.** `ci.yml`'s
  `publish` job runs a dry-run version of itself (no pack, no push) on the 1st of every month and on
  manual `workflow_dispatch`, gated by `IS_DRY_RUN`. If NuGet's policy changes, or `NuGet/login`
  changes its output contract again, this fails loudly (auto-filing/reusing a GitHub issue) within
  a month instead of at the next real release. See `ROADMAP.md`'s `6.4.3` entry for why this has to
  live inside `ci.yml` itself rather than a separate workflow file (NuGet's Trusted Publishing
  policy is scoped to the exact workflow filename).
