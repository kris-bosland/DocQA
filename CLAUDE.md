# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

DocQA — upload `.txt`/`.pdf` documents, ask natural-language questions, get answers from Claude with a cited excerpt. Blazor WebAssembly client + ASP.NET Core Minimal API server + shared DTO library, all .NET 10. Solution file is `DocQA.slnx` (XML solution format, not `.sln`).

Full spec: `docs/docqa_project_spec.md`. Phased build plan: `docs/implementation-plan.md`.

## Commands

```powershell
dotnet build                                    # whole solution
dotnet run --project DocQA.Server               # API at https://localhost:7221
dotnet run --project DocQA.Client               # client at https://localhost:7149

# API key (once per machine — required for real Claude calls; tests stub it)
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project DocQA.Server

# Tests — CI runs the three projects separately; browser tests need Playwright installed first
dotnet test DocQA.Tests.Unit
dotnet test DocQA.Tests.Acceptance
dotnet test DocQA.Tests.Browser

# Single test / subset
dotnet test DocQA.Tests.Unit --filter "FullyQualifiedName~ClaudeServiceTests"
dotnet test DocQA.Tests.Unit --filter "Name=QueryDocumentAsync_ContentTruncatedAt150KCharacters"

# Playwright browsers (once, before first browser-test run)
pwsh DocQA.Tests.Browser/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium

# EF Core migrations — existing migrations live in Data/Migrations, not the default Migrations/
dotnet ef migrations add <Name> --project DocQA.Server
dotnet ef database update --project DocQA.Server
```

There is no linter or formatter configured beyond the compiler; nullable reference types and implicit usings are on everywhere.

## Architecture

**Request flow**: `Index.razor`/`DocumentChat.razor` → `ApiClient` (client's only `HttpClient` consumer) → Minimal API endpoint extension methods → `IDocumentService`/`IClaudeService` → EF Core / Anthropic SDK.

**The `IMessageSender` seam is the key testability decision.** `ClaudeService` owns prompt construction, 150k-char content truncation, `"Excerpt:"` response parsing, and exception classification — but never touches the network. `AnthropicMessageSender` is the only class that talks to `Anthropic.SDK`. Unit tests mock `IMessageSender` to test `ClaudeService` logic; acceptance and browser tests replace `IClaudeService` wholesale with a `StubClaudeService`. When changing Claude behaviour, decide which of these two layers it belongs in.

**Error-code contract between server and client.** `ClaudeService` translates SDK exceptions into `ClaudeAuthenticationException` / `ClaudeUnavailableException`; `QueryEndpoints` maps those to a `QueryErrorResponse` JSON payload with an explicit `Code` (`CLAUDE_AUTH_FAILED` → 502, `CLAUDE_UNAVAILABLE` → 503, `QUERY_PROCESSING_FAILED` → 500); `ApiClient.QueryDocumentAsync` reads the code back and throws a typed `ApiException` so the UI can distinguish "can't reach the server" from "server can't reach Claude" from "bad API key". Adding a failure mode means touching all three places.

**Persistence**: SQLite via EF Core, code-first. `Program.cs` runs `db.Database.Migrate()` at startup, so a schema change needs a committed migration or production breaks. `DocumentService.DeleteAsync` uses `ExecuteDeleteAsync`, which bypasses the change tracker — cascade deletion of `Message` rows relies on the DB-level `ON DELETE CASCADE` configured in `AppDbContext.OnModelCreating`.

**Content loading is deliberately asymmetric**: `GetAllAsync` projects `Content = null` (list views never carry document bodies); `GetByIdAsync` includes it. Preserve that split when adding queries.

**Client configuration is baked into the deployed asset.** The client reads `ApiBaseUrl` and `BuildVersion` from `DocQA.Client/wwwroot/appsettings.json`, which is committed pointing at the **deployed Azure API**, not localhost. Running the client locally against a local server means temporarily changing that value (and not committing it). `About.razor` shows client `BuildVersion` plus the server's version and active Claude model, fetched from `GET /api/system/info`.

**CORS** is allow-listed in `Program.cs`: explicit origins plus any `*.azurestaticapps.net` or `localhost` host.

## Test layers

| Project | Scope | Isolation mechanism |
|---|---|---|
| `DocQA.Tests.Unit` | `ClaudeService`, `DocumentService`, `PdfTextExtractor`, `ApiClient` | Moq + EF In-Memory; PDF fixtures are embedded resources in `TestData/` |
| `DocQA.Tests.Acceptance` | HTTP-level endpoint behaviour | `WebApplicationFactory<Program>` + shared `:memory:` SQLite connection + `StubClaudeService` |
| `DocQA.Tests.Browser` | End-to-end UI flows in Chromium | Custom `BrowserFixture` (not `WebApplicationFactory`) |

`Program.cs` ends with `public partial class Program { }` solely so `WebApplicationFactory<Program>` can bind — don't remove it.

`BrowserFixture` shells out to `dotnet publish DocQA.Client` and serves the published `wwwroot` from a temp copy, overwriting only `appsettings.json` with the test host's URL. This makes browser tests slow but exact. If Blazor assets 404 or the page stays blank, check the publish layout and static-file mappings before touching test code — `.dat` must be mapped to `application/octet-stream` for ICU globalization assets. Browser tests are serialized via `BrowserCollection`; keep them that way, they share one server fixture. See `.github/instructions/playwright.instructions.md` and `memories/repo/playwright-browser-hosting.md`.

## Conventions

Per-directory conventions live in `.github/instructions/` (`server`, `client`, `shared`, `playwright`) and are the authoritative style reference — read the relevant one before editing that project. In summary:

- **Minimal API only**, no MVC controllers. Endpoints are `static` extension methods on `IEndpointRouteBuilder` in `Endpoints/`, registered from `Program.cs`. Return `Results<...>` union types with `TypedResults`.
- **Interface before implementation** for services; inject `ILogger<T>`; primary constructors throughout.
- **Never return EF models over the wire** — map to DTOs in `DocQA.Shared`. Naming: `{Entity}Dto`, `{Verb}{Entity}Request`, `{Verb}{Entity}Response`. DTOs hold no logic and no EF attributes.
- **Async with `CancellationToken`** on all I/O; `AsNoTracking()` for reads; `DateTime.UtcNow` never `DateTime.Now`.
- **C# only in the client** — no JavaScript interop, no JS frameworks. Components inject `ApiClient`, never `HttpClient` directly; every new endpoint gets an `ApiClient` method.
- **Secrets** via User Secrets locally and env vars (`Anthropic__ApiKey`) in Azure.
- Out of scope for v1: auth, formats beyond `.txt`/`.pdf`, streaming responses, multi-document queries.

## Things to be careful with

- There are two CI definitions: `.github/workflows/azure-static-web-apps-*.yml` (the live one — build, unit + acceptance tests, deploy API to App Service and client to Static Web Apps) and `.gitea/workflows/ci.yml` (a Gitea variant that additionally runs browser tests). Browser tests do **not** run in the GitHub pipeline.
- `DocQA.Client/Pages/` still contains unused template leftovers `Counter.razor` and `Weather.razor`.

## Git

The repository owner manages all git operations. Do not commit, push, branch, checkout, or merge unless explicitly asked.
