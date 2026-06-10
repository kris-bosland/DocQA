# DocQA — Staged Implementation Plan

---

## Guiding Principles

- Get a **green CI build with skeleton tests** before writing any feature code
- Every phase ends with a passing build; nothing merges to `main` while CI is red
- Tests are written (or stubs committed) before the implementation they cover
- Unit tests mock external dependencies (EF, Claude API); acceptance tests use real infrastructure (in-process or real DB)
- Gitea Actions handles CI throughout development; see deployment notes for GitHub Actions migration

---

## Test Project Structure

```
DocQA/
├── DocQA.Tests.Unit/           ← xUnit; fast; no I/O; mock everything external
│   ├── Services/
│   │   ├── DocumentServiceTests.cs
│   │   └── ClaudeServiceTests.cs
│   └── Parsing/
│       └── PdfTextExtractorTests.cs
├── DocQA.Tests.Acceptance/     ← xUnit + WebApplicationFactory; hits real SQLite DB
│   ├── DocumentsApiTests.cs    ← HTTP-level tests against the running API
│   ├── QueryApiTests.cs
│   └── Fixtures/
│       └── ApiFixture.cs       ← shared WebApplicationFactory<Program> setup
└── DocQA.Tests.Browser/        ← xUnit + Playwright; real browser against a live Kestrel port
    ├── IndexPageTests.cs
    ├── DocumentChatTests.cs
    └── Fixtures/
        └── BrowserFixture.cs   ← starts Kestrel on a fixed port; launches Chromium
```

**Unit tests** (`DocQA.Tests.Unit`)
- Reference: `DocQA.Server`, `DocQA.Shared`
- NuGet: `xunit`, `xunit.runner.visualstudio`, `Moq`, `Microsoft.EntityFrameworkCore.InMemory`
- No network I/O, no file system

**Acceptance tests** (`DocQA.Tests.Acceptance`)
- Reference: `DocQA.Server`, `DocQA.Shared`
- NuGet: `xunit`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Sqlite`
- Spin up `DocQA.Server` in-process with SQLite (`:memory:`) and a stubbed `IClaudeService`
- Test full HTTP request/response cycle including DB round-trips

**Browser tests** (`DocQA.Tests.Browser`)
- Reference: `DocQA.Server`
- NuGet: `xunit`, `xunit.runner.visualstudio`, `Microsoft.Playwright`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Sqlite`
- Starts a real Kestrel server on a fixed port (not the in-process TestServer) so Playwright can connect
- Uses SQLite `:memory:` and `StubClaudeService` — same isolation strategy as acceptance tests
- Headless Chromium in CI; headed locally for debugging

---

## Phase 0 — Project Skeleton + Green CI

**Goal**: empty solution compiles, skeleton test projects run (no real tests yet), Gitea Actions reports green.

### Tasks

1. Create solution and projects
   ```
   dotnet new sln -n DocQA
   dotnet new webapi -n DocQA.Server --no-openapi
   dotnet new blazorwasm -n DocQA.Client --hosted   # or add manually
   dotnet new classlib -n DocQA.Shared
   dotnet new xunit -n DocQA.Tests.Unit
   dotnet new xunit -n DocQA.Tests.Acceptance
   dotnet new xunit -n DocQA.Tests.Browser
   dotnet sln add DocQA.Server DocQA.Client DocQA.Shared DocQA.Tests.Unit DocQA.Tests.Acceptance DocQA.Tests.Browser
   ```
2. Add project references:
   - `DocQA.Server` → `DocQA.Shared`
   - `DocQA.Client` → `DocQA.Shared`
   - `DocQA.Tests.Unit` → `DocQA.Server`, `DocQA.Shared`
   - `DocQA.Tests.Acceptance` → `DocQA.Server`, `DocQA.Shared`
   - `DocQA.Tests.Browser` → `DocQA.Server`
3. Add `[Fact(Skip = "skeleton")]` placeholder tests in each test project so the runner exits 0
4. Commit `.gitea/workflows/ci.yml` (see below)
5. Push and verify Gitea Actions reports green

### Gitea Actions CI workflow

```yaml
# .gitea/workflows/ci.yml
#
# GITEA vs GITHUB ACTIONS MIGRATION NOTES
# ----------------------------------------
# This file uses Gitea Actions syntax, which is a subset of GitHub Actions YAML.
# When migrating to GitHub Actions:
#   1. Move this file to .github/workflows/ci.yml (no content changes needed for
#      the build/test jobs — Gitea Actions supports the same syntax).
#   2. Change `runs-on` if your Gitea runner label differs from GitHub's hosted
#      runner names. GitHub hosted runners use: ubuntu-latest, windows-latest, macos-latest.
#   3. GitHub marketplace actions (e.g. azure/webapps-deploy) work natively on
#      GitHub Actions but are fetched from GitHub by Gitea runners — ensure your
#      Gitea runner has outbound internet access, or pin to a local mirror.
#   4. Add deployment jobs (see Phase 6 notes) — they are omitted here because
#      they depend on Azure resources that do not exist yet.

name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    # MIGRATION NOTE: 'ubuntu-latest' works on both Gitea (if runner is labelled
    # ubuntu-latest) and GitHub hosted runners. Adjust label to match your Gitea
    # runner registration if needed (e.g. 'self-hosted', 'linux').
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Run unit tests
        run: >
          dotnet test DocQA.Tests.Unit/DocQA.Tests.Unit.csproj
          --no-build --configuration Release
          --logger "console;verbosity=normal"

      - name: Run acceptance tests
        run: >
          dotnet test DocQA.Tests.Acceptance/DocQA.Tests.Acceptance.csproj
          --no-build --configuration Release
          --logger "console;verbosity=normal"
        env:
          # Acceptance tests use SQLite :memory: — no connection string needed.
          # MIGRATION NOTE: if you switch acceptance tests to SQL Server LocalDB
          # in CI, add the connection string as a GitHub/Gitea secret and
          # reference it here: ConnectionStrings__DefaultConnection: ${{ secrets.CI_DB }}
          ASPNETCORE_ENVIRONMENT: Testing

      # Browser tests are disabled until Phase 5b.
      # To enable: remove the 'if: false' condition and ensure
      # DocQA.Tests.Browser has real tests (no Skip attributes).
      # MIGRATION NOTE: the install command and test step below are identical
      # on GitHub Actions — no changes needed when migrating.
      - name: Install Playwright browsers
        if: false
        run: pwsh DocQA.Tests.Browser/bin/Release/net9.0/playwright.ps1 install --with-deps chromium

      - name: Run browser tests
        if: false
        run: >
          dotnet test DocQA.Tests.Browser/DocQA.Tests.Browser.csproj
          --no-build --configuration Release
          --logger "console;verbosity=normal"
        env:
          ASPNETCORE_ENVIRONMENT: Testing
```

**Deliverable**: `main` branch builds green; no feature code yet.

---

## Phase 1 — Data Layer

**Goal**: EF Core models, `AppDbContext`, and first migration. Unit tests for model constraints pass.

### Tasks

1. Install NuGet packages in `DocQA.Server`:
   - `Microsoft.EntityFrameworkCore.SqlServer`
   - `Microsoft.EntityFrameworkCore.Design`
   - `Microsoft.EntityFrameworkCore.Sqlite` (for acceptance test configuration)
   - `Microsoft.EntityFrameworkCore.Tools`
2. Create `DocQA.Server/Models/Document.cs` and `Message.cs`
3. Create `DocQA.Server/Data/AppDbContext.cs` (cascade delete on `Message → Document`)
4. Register `AppDbContext` in `Program.cs`; read connection string from config
5. Run first migration:
   ```bash
   dotnet ef migrations add InitialCreate --project DocQA.Server
   dotnet ef database update --project DocQA.Server
   ```
6. Write unit tests in `DocQA.Tests.Unit/Services/` using EF in-memory provider to verify:
   - Cascade delete removes messages when a document is deleted
   - `Message.Role` accepts `"user"` and `"assistant"`
7. Wire up `ApiFixture` in `DocQA.Tests.Acceptance` to configure SQLite `:memory:` and run `EnsureCreated()`

### Acceptance test fixture skeleton

```csharp
// DocQA.Tests.Acceptance/Fixtures/ApiFixture.cs
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with SQLite :memory:
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlite("DataSource=:memory:"));

            // Replace IClaudeService with a stub
            var claude = services.Single(d => d.ServiceType == typeof(IClaudeService));
            services.Remove(claude);
            services.AddSingleton<IClaudeService, StubClaudeService>();
        });
    }

    public async Task InitializeAsync()
    {
        // Open the SQLite connection before EnsureCreated (required for :memory:)
        var db = Services.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync() => await base.DisposeAsync();
}
```

**Deliverable**: `dotnet test` green; migrations present; no endpoints yet.

---

## Phase 2 — Document Endpoints (txt only)

**Goal**: full upload → list → get → delete cycle working for `.txt` files; acceptance tests pass.

### Tasks

1. Create `IDocumentService` and `DocumentService` (`.txt` parsing only — read as UTF-8 string)
2. Create `DocQA.Server/Endpoints/DocumentEndpoints.cs`:
   - `POST /api/documents`
   - `GET /api/documents`
   - `GET /api/documents/{id}`
   - `DELETE /api/documents/{id}`
3. Add DTOs to `DocQA.Shared` (`DocumentDto`)
4. Register service and endpoints in `Program.cs`
5. Write acceptance tests (`DocumentsApiTests.cs`):
   - Upload a `.txt` file → 201, ID returned
   - `GET /api/documents` returns the uploaded file in the list
   - `GET /api/documents/{id}` returns metadata + content
   - `DELETE /api/documents/{id}` → 204, subsequent GET returns 404
   - Upload with no file → 400
   - Upload a file type other than `.txt`/`.pdf` → 400

**Deliverable**: 5+ acceptance tests passing; txt upload cycle complete.

---

## Phase 3 — PDF Parsing

**Goal**: `.pdf` uploads extract text via PdfPig; existing acceptance tests continue to pass.

### Tasks

1. Install `UglyToad.PdfPig` in `DocQA.Server`
2. Add a static `PdfTextExtractor.ExtractText(Stream)` helper (or fold into `DocumentService`)
3. Update `DocumentService.ParseContentAsync` to branch on file extension
4. Unit tests in `DocQA.Tests.Unit/Parsing/PdfTextExtractorTests.cs`:
   - Embed a small test PDF as an assembly resource
   - Assert extracted text contains known strings
   - Assert empty PDF returns empty string without throwing
5. Add acceptance test: upload a `.pdf` file → content is non-empty

**Deliverable**: pdf + txt uploads both pass; unit tests for extraction pass.

---

## Phase 4 — Claude Integration + Query Endpoint

**Goal**: question → answer + excerpt cycle works end-to-end (with stubbed Claude in acceptance tests).

### Tasks

1. Create `IClaudeService` and `ClaudeService` (calls Anthropic SDK)
2. `ClaudeService.QueryDocumentAsync(content, question, ct)` — truncate to 150K chars, return `(Answer, Excerpt)`
3. Create `DocQA.Server/Endpoints/QueryEndpoints.cs`:
   - `POST /api/documents/{id}/query`
   - `GET /api/documents/{id}/messages`
4. Add DTOs: `QueryRequest`, `QueryResponse`
5. Register in `Program.cs`
6. Unit tests (`ClaudeServiceTests.cs`) with mocked `Anthropic.SDK` client:
   - Verify system prompt contains "Answer based only on the document content"
   - Verify content is truncated at 150K characters
   - Verify response is parsed correctly into `(Answer, Excerpt)`
7. Acceptance tests (`QueryApiTests.cs`) using `StubClaudeService`:
   - POST question to unknown document → 404
   - POST question → 200 with `answer`, `excerpt`, `documentId`, `messageId`
   - GET messages → returns user + assistant messages in order
   - Verify messages are persisted (GET again → same count)

**Deliverable**: full upload → ask → answer loop passing in acceptance tests.

---

## Phase 5 — Blazor Client

**Goal**: working UI; wired to the API. Manually verified end-to-end; browser tests added in Phase 5b.

### Tasks

1. Set up `ApiClient.cs` (typed `HttpClient` wrapper) with methods matching all endpoints
2. Implement `Index.razor`:
   - Load and display document list on init
   - Upload file, refresh list on success
   - Delete document, remove from list
3. Implement `DocumentChat.razor`:
   - Load document name on init
   - Send question, show optimistic user bubble, append assistant reply
   - Display message history on load
4. Configure `HttpClient` base address in `DocQA.Client/Program.cs`
5. Manual smoke-test locally against the running server

**Deliverable**: app works end-to-end in a browser locally; all existing CI tests still pass.

---

## Phase 5b — Browser Tests (Playwright)

**Goal**: key user journeys covered by headless browser tests running in CI before going live.

### What Playwright requires

| Requirement | Detail |
|---|---|
| NuGet package | `Microsoft.Playwright` (includes the `playwright.ps1` install script) |
| Browser binaries | Downloaded at build time via `playwright.ps1 install --with-deps chromium` (~170 MB, cached between CI runs) |
| Real network port | Playwright cannot use ASP.NET's in-process `TestServer` — the app must bind to a real Kestrel socket |
| `pwsh` available | PowerShell Core — present by default on `ubuntu-latest` runners |

### Hosting strategy

`BrowserFixture` starts a real Kestrel server alongside the in-process `TestServer` so Playwright has a URL to connect to:

```csharp
// DocQA.Tests.Browser/Fixtures/BrowserFixture.cs
public class BrowserFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string BaseUrl = "http://localhost:5099"; // fixed port; must not conflict
    private IHost? _kestrelHost;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string AppBaseUrl => BaseUrl;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace DbContext with SQLite :memory:
            var db = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(db);
            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite("DataSource=:memory:"));

            // Replace IClaudeService with stub
            var claude = services.Single(d => d.ServiceType == typeof(IClaudeService));
            services.Remove(claude);
            services.AddSingleton<IClaudeService, StubClaudeService>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Start the real Kestrel host on the fixed port for Playwright
        var kestrelBuilder = builder.ConfigureWebHost(b =>
            b.UseKestrel().UseUrls(BaseUrl));
        _kestrelHost = kestrelBuilder.Build();
        _kestrelHost.Start();

        // Also build the TestServer host (required by WebApplicationFactory infrastructure)
        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient(); // triggers CreateHost
        var db = _kestrelHost!.Services.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public new async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
        if (_kestrelHost is not null) await _kestrelHost.StopAsync();
        await base.DisposeAsync();
    }
}
```

### Tasks

1. Install NuGet in `DocQA.Tests.Browser`:
   - `Microsoft.Playwright`
   - `Microsoft.AspNetCore.Mvc.Testing`
   - `Microsoft.EntityFrameworkCore.Sqlite`
2. Implement `BrowserFixture` (above)
3. Write browser tests:

   **`IndexPageTests.cs`**
   - Page loads; document table is visible
   - Upload a `.txt` file → new row appears in the table
   - Click [Delete] → row is removed
   - Upload a `.pdf` file → new row appears (verifies PDF path end-to-end)

   **`DocumentChatTests.cs`**
   - Click [Open] on a document → navigates to `/document/{id}`
   - Empty state message is displayed before any question is asked
   - Type a question and click [Ask] → user bubble appears immediately
   - Stub returns a canned answer → assistant bubble appears with that answer

4. Enable the browser test steps in `.gitea/workflows/ci.yml` by removing the `if: false` conditions
5. Add Playwright browser caching to CI to avoid re-downloading on every run:

```yaml
      # Add before the 'Install Playwright browsers' step
      # MIGRATION NOTE: actions/cache works identically on GitHub Actions
      - name: Cache Playwright browsers
        uses: actions/cache@v4
        with:
          path: ~/.cache/ms-playwright
          key: playwright-chromium-${{ runner.os }}-${{ hashFiles('**/DocQA.Tests.Browser/*.csproj') }}
```

**Deliverable**: browser tests green in CI; `if: false` guards removed; ready to deploy.

---

## Phase 6 — Azure Deployment + CI/CD

**Goal**: `main` push deploys to Azure automatically using SQLite as the production database.

### SQLite in production

App Service (Linux) provides a persistent `/home` directory that survives restarts on a single-instance plan. The SQLite database file lives there.

- Connection string (set as App Service environment variable): `DataSource=/home/docqa/docqa.db`
- EF migrations run at startup via `db.Database.Migrate()` in `Program.cs`
- **Limitation**: SQLite does not support scale-out (multiple instances). The F1 free tier is single-instance, so this is safe for v1. Document in the README if this changes.

### Tasks

1. Switch the production EF provider from `SqlServer` to `Sqlite` in `DocQA.Server`:
   - Remove `Microsoft.EntityFrameworkCore.SqlServer`
   - Keep `Microsoft.EntityFrameworkCore.Sqlite` (already present for tests)
   - Update `Program.cs` to call `UseSqlite(connectionString)`
   - Re-run migrations: `dotnet ef migrations add UseSqlite --project DocQA.Server`
2. Provision Azure resources in `docqa-rg`:
   - App Service F1 — hosts `DocQA.Server` (no Azure SQL needed)
   - Static Web App free — hosts `DocQA.Client` (Blazor WASM)
3. Set App Service environment variables in the portal:
   - `ConnectionStrings__DefaultConnection` = `DataSource=/home/docqa/docqa.db`
   - `ANTHROPIC_API_KEY` = your key (never in CI secrets)
3. Add deploy jobs to `.gitea/workflows/ci.yml` (and `.github/workflows/ci.yml` when migrating):

```yaml
# Add to ci.yml AFTER provisioning Azure resources
# 
# MIGRATION NOTE: The two deploy actions below are GitHub-published actions
# (azure/webapps-deploy, Azure/static-web-apps-deploy). They run identically
# on GitHub Actions. On Gitea Actions, they are fetched from GitHub at runtime;
# ensure your Gitea runner has outbound internet access or cache them locally.
#
# Secrets to add in Gitea repo settings → Actions → Secrets:
#   AZURE_WEBAPP_PUBLISH_PROFILE   — paste XML from App Service portal
#   AZURE_STATIC_WEB_APPS_API_TOKEN — from Static Web App portal

  deploy-api:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Publish server
        run: dotnet publish DocQA.Server/DocQA.Server.csproj -c Release -o publish/server
      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: docqa-server        # MIGRATION NOTE: same action on GitHub Actions
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: publish/server

  deploy-client:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Publish client
        run: dotnet publish DocQA.Client/DocQA.Client.csproj -c Release -o publish/client
      - name: Deploy to Static Web App
        uses: Azure/static-web-apps-deploy@v1
        with:                           # MIGRATION NOTE: same action on GitHub Actions
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          action: upload
          app_location: publish/client/wwwroot
          skip_app_build: true
```

4. Update README with connection string format and deployment instructions
5. Verify deployed app end-to-end

**Deliverable**: push to `main` → green CI → live on Azure.

---

## Summary Table

| Phase | Deliverable | Test gate |
|---|---|---|
| 0 | Empty solution + green CI | Build passes, skeleton tests exit 0 |
| 1 | EF models + migration + AppDbContext | Unit tests for cascade delete pass |
| 2 | Document CRUD endpoints (txt) | 5+ acceptance tests pass |
| 3 | PDF parsing | Unit + acceptance tests for pdf upload pass |
| 4 | Claude query endpoint | Unit tests (mocked) + acceptance tests (stubbed) pass |
| 5 | Blazor client | Manual smoke test; all existing CI tests pass |
| 5b | Browser tests (Playwright) | Browser tests green in CI; `if: false` guards removed |
| 6 | Azure deployment + CI/CD (SQLite) | Deployed app accessible; CI deploys on push to main |
