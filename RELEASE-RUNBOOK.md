# Release runbook

> **Audience:** the maintainer, cutting a release. Not customer-facing. See
> `Junaid.GoogleGemini.Net/RELEASE.md`'s `PackageReleaseNotes` for that.

This file exists because of `v6.4.2`. Its first tag push looked fine by every visible signal. CI
was green. But it actually published nothing. NuGet Trusted Publishing's login step reported
success. It handed back an empty API key under the wrong output name. Nothing in the process at
the time would have caught that without reading the raw log. Step 6 below is the one that matters
most.

## Steps

1. **Run the tests that actually catch regressions first.**
   - Unit suite: `dotnet test tests/Junaid.GoogleGemini.Net.Tests/Junaid.GoogleGemini.Net.Tests.csproj -c Release`
   - Live suite, against a real key: `GeminiApiKey=<key> dotnet test tests/Junaid.GoogleGemini.Net.IntegrationTests/Junaid.GoogleGemini.Net.IntegrationTests.csproj -c Release --filter "FullyQualifiedName!~Batch"`
   - Add `GeminiPaidTier=1` to the same command if the key is billing-enabled. This also covers
     image generation and context caching. Do not guess the key's tier from a self-skip. A skipped
     paid-tier test only means this flag was not set. It does not mean the key is free-tier. Add
     `--filter "FullyQualifiedName~Batch"` separately to cover Batch. It has a real per-job cost and
     takes about 90 seconds to poll.
   - `dotnet build -c Release` on all three targets should show 0 warnings. `TreatWarningsAsErrors`
     is on for the core package.

2. **Bump the version and write real release notes.** Do not leave a stub.
   - Update `<Version>` in `Junaid.GoogleGemini.Net/Junaid.GoogleGemini.Net.csproj`.
   - Add a new entry to that file's `<PackageReleaseNotes>`. This is what NuGet.org shows, so it is
     the customer-facing changelog. Keep the older entries. Do not delete them.
   - Add a matching entry to `ROADMAP.md` under "Phase 4." This is the fuller, maintainer-facing
     account: what broke, how it was found, how it was verified. This repo's habit is to
     over-document here, not under-document.

3. **Open a PR and get it merged.** Branch protection needs one approving review. That is by
   design. Do not try to route around it, and do not ask an agent to either.

4. **Tag it and push the tag:**
   ```
   git checkout master && git pull --ff-only origin master
   git tag -a vX.Y.Z -m "..."
   git push origin vX.Y.Z
   ```

5. **Watch CI. Check both jobs, not just the summary.**
   Run `gh run list --workflow=ci.yml --limit 2` to find the tag-triggered run. Run
   `gh run view <id>` until both `Build & Test` and `Publish to NuGet` show a checkmark.

6. **Verify the actual push. Do not trust the checkmark alone.** This is the step `v6.4.2` skipped,
   and the one that would have caught the bug right away:
   ```
   gh run view <id> --log | grep -E "Pushing|Created|Conflict|error"
   ```
   Look for the literal `Created https://www.nuget.org/api/v2/package/ ...` line and the
   `Your package was pushed.` line, for the package you are releasing. A sibling package that
   already exists will correctly show `Conflict` and "already exists," under `--skip-duplicate`.
   That is success, not failure. A green job with no `Created` line for your package is the exact
   failure this step exists to catch.

7. **Poll nuget.org until the package is actually downloadable.** A successful push and a
   propagated, downloadable package are two different facts. NuGet's CDN and search index can lag
   the raw upload by a few minutes:
   ```
   until curl -s -o /dev/null -w "%{http_code}" \
     "https://api.nuget.org/v3-flatcontainer/junaid.googlegemini.net/X.Y.Z/junaid.googlegemini.net.x.y.z.nupkg" \
     | grep -q "^200$"; do sleep 15; done
   ```
   Use lowercase for the version in the URL. Do not call the release done before this returns 200.

8. **Create the GitHub Release.** Match the style of prior releases. Run `gh release view vPREV` to
   see it: numbered highlights, a build and test line, a note on the sibling package, and a link to
   `ROADMAP.md`.
   ```
   gh release create vX.Y.Z --title "vX.Y.Z" --notes-file <path>
   ```
   It is marked "Latest" automatically, since it is the newest non-prerelease release.

9. **Clean up.** Delete the merged branch, on your machine and on GitHub:
   ```
   git branch -d <branch> && git push origin --delete <branch>
   ```

## What this runbook does not cover, on purpose

- **Bypassing branch-protection review.** This is not automatable by design. If an agent is asked
  to "just merge it," it should refuse and explain why. It should not look for a workaround.
- **A staging or test NuGet feed.** There is not one. `--skip-duplicate` plus step 6 above are the
  safety net instead.
- **Rotating or storing a NuGet API key.** Trusted Publishing (OIDC, through `NuGet/login@v1.2.0`
  in `ci.yml`) means there is nothing to rotate. But read the next point too.
- **Catching a break in Trusted Publishing before it blocks a real release.** `ci.yml`'s `publish`
  job also runs a dry-run version of itself on the first of every month, and whenever you dispatch
  it by hand. The dry run does not pack or push anything, and is controlled by `IS_DRY_RUN`. If
  NuGet's policy changes, or `NuGet/login` changes its output again, this fails loudly and files or
  reuses a GitHub issue, within a month instead of at the next real release. See `ROADMAP.md`'s
  `6.4.3` entry for why this check has to live inside `ci.yml` itself, not a separate workflow file.
  NuGet's Trusted Publishing policy is scoped to the exact workflow filename.
