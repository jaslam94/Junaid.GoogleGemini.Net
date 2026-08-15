# Security Policy

## Supported Versions

Only the latest patch release of each package receives security fixes. There is no long-term
support branch: given how quickly the Gemini API surface moves, security and correctness fixes
land on top of the current major version, not backported to older ones.

| Package | Supported |
|---|---|
| `Junaid.GoogleGemini.Net` >= 6.4.1 | Yes |
| `Junaid.GoogleGemini.Net` < 6.4.1 | No, upgrade |
| `Junaid.GoogleGemini.Net.Extensions.AI` >= 6.2.0 | Yes |
| `Junaid.GoogleGemini.Net.Extensions.AI` < 6.2.0 | No, upgrade |
| 5.x (pre-modernization) | No |

If you're on an older 6.x release, check the [changelog](ROADMAP.md) before upgrading, v6 is a
modernization release with breaking changes.

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Instead, report privately using one of these channels:

1. **Preferred:** [GitHub Security Advisories](https://github.com/jaslam94/Junaid.GoogleGemini.Net/security/advisories/new)
   for this repository. This keeps the report private until a fix ships and lets us collaborate
   on a coordinated disclosure and CVE if warranted.
2. **Email:** aslam.junaid786@hotmail.com with a subject line starting `SECURITY:`.

Please include:

- A description of the vulnerability and its potential impact.
- Steps to reproduce, or a minimal repro project/snippet.
- The affected version(s).
- Whether the issue is in this library's code or in how it wraps the underlying Gemini API.

### What to expect

- **Acknowledgment:** within 3 business days.
- **Triage:** I'll confirm whether it's in scope and its severity, and let you know the plan.
- **Fix:** for confirmed vulnerabilities, I aim to ship a patch release and publish an advisory
  within 14 days of confirmation, sooner for critical issues. Complex fixes may take longer; I'll
  keep you updated.
- **Credit:** with your permission, you'll be credited in the advisory and release notes.

## Scope

In scope:

- The library's own code: HTTP handling, serialization, auth header/key handling, rate limiting,
  cost governance, retry/resilience logic, and the `IChatClient`/`IEmbeddingGenerator` adapters.
- Dependency vulnerabilities introduced via this library's package references.

Out of scope:

- Vulnerabilities in the Google Gemini API itself, if you find one there, report it to
  [Google's Vulnerability Reward Program](https://bughunters.google.com/) instead.
- Issues that require an attacker to already have your Gemini API key or full control of your
  process (that's the same trust boundary as any other HTTP client library).
