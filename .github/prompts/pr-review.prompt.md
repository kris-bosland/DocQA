---
description: "Review a pull request for correctness, conventions, and security. Use when: reviewing a PR, code review, checking a branch before merge."
mode: agent
model: "o3 (copilot)"
argument-hint: "Paste the PR description, or attach the diff with #changes"
tools: [read, search]
---

Review this pull request against the DocQA project conventions.
The input of the prompt will be the PR description, or a diff of the changed files if the description is insufficient.
Also read any repo files in place on disk if needed for context or if parts of the diff are ambiguous, incomplete, or missing.

## What to check

**Correctness**
- Does the code do what the PR description says it does?
- Are there logic errors, off-by-one mistakes, or unhandled edge cases?
- For endpoints: are 400/404 responses returned in the right places?

**Conventions** (from [copilot-instructions.md](../copilot-instructions.md))
- Endpoints registered as extension methods in `Endpoints/` — no controllers
- Services have interfaces (`IDocumentService`, `IClaudeService`)
- All I/O is `async Task<T>` with `CancellationToken`
- DTOs are in `DocQA.Shared` — EF models never returned directly from endpoints
- No secrets committed; no JavaScript added to the client

**Security (OWASP)**
- No SQL injection — EF parameterised queries only
- File upload: extension validated (`.txt`/`.pdf` only), size not unbounded
- No sensitive data (API keys, connection strings) in committed code or logs
- CDN resources use SRI integrity attributes

**Tests**
- Are new service methods covered by a unit test?
- Are new endpoints covered by an acceptance test in `DocQA.Tests.Acceptance`?
- Do existing tests still pass (no regressions introduced)?

**EF / database**
- Read-only queries use `AsNoTracking()`
- New relationships configure cascade delete where appropriate
- Migrations included if the data model changed

## Output format

Provide your review as:

1. **Summary** — one paragraph on overall quality and readiness to merge
2. **Must fix** — blocking issues (bugs, security problems, missing tests)
3. **Should fix** — non-blocking but important (convention violations, missing edge case handling)
4. **Suggestions** — optional improvements (readability, minor refactors)
5. **Verdict** — `Approve`, `Request changes`, or `Comment`

---

PR content to review:
