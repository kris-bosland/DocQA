# DocQA — Copilot Instructions

Document Q&A tool. Users upload `.txt`/`.pdf` files, then ask natural-language questions answered by Claude AI with a cited excerpt.

Full specification: [docs/docqa_project_spec.md](../docs/docqa_project_spec.md)

---

## Tech Stack

| Layer | Technology |
|---|---|
| Front-end | Blazor WebAssembly (.NET 10, C# only — no JS frameworks) |
| API | ASP.NET Core Minimal API (.NET 10) — no MVC controllers |
| AI | Anthropic Claude API via `Anthropic.SDK` NuGet — model from `Anthropic:Model`, default `claude-haiku-4-5` |
| ORM | Entity Framework Core 10 (code-first, migrations) |
| Database (dev) | SQLite (`DataSource=docqa.db`) |
| Database (prod) | SQLite on App Service (`DataSource=/home/docqa.db`) |
| PDF parsing | PdfPig (NuGet, pure .NET) |
| Testing | xUnit + Moq (unit), `WebApplicationFactory` (acceptance), Playwright (browser) |
| Hosting | Azure App Service (API) + Azure Static Web Apps (Blazor WASM) |
| CI/CD | GitHub Actions (`.github/workflows/`); a Gitea Actions variant lives in `.gitea/workflows/` |

---

## Solution Structure

```
DocQA/
├── DocQA.slnx              ← XML solution format (not .sln)
├── DocQA.Server/           ← ASP.NET Core host + Minimal API
│   ├── Endpoints/          ← DocumentEndpoints.cs, QueryEndpoints.cs, SystemEndpoints.cs
│   ├── Services/           ← IDocumentService/DocumentService, IClaudeService/ClaudeService,
│   │                          IMessageSender/AnthropicMessageSender, Claude*Exception
│   ├── Parsing/            ← PdfTextExtractor.cs
│   ├── Data/               ← AppDbContext, Migrations/
│   └── Models/             ← Document.cs, Message.cs
├── DocQA.Client/           ← Blazor WebAssembly
│   ├── Pages/              ← Index.razor (list/upload), DocumentChat.razor, About.razor
│   ├── Services/           ← ApiClient.cs (typed HttpClient wrapper)
│   └── Layout/             ← MainLayout.razor
├── DocQA.Shared/           ← DTOs: DocumentDto, MessageDto, QueryRequest, QueryResponse,
│                              QueryErrorResponse, SystemInfoDto
├── DocQA.Tests.Unit/       ← xUnit + Moq; EF In-Memory
├── DocQA.Tests.Acceptance/ ← WebApplicationFactory<Program>; :memory: SQLite + StubClaudeService
└── DocQA.Tests.Browser/    ← Playwright; custom BrowserFixture serving published client output
```

---

## Coding Conventions

- **Minimal API only** — register endpoints as extension methods in `Endpoints/`, no controllers
- **Interfaces for services** — `IDocumentService` / `IClaudeService` for testability
- **Async throughout** — all I/O methods are `async Task<T>` with `CancellationToken`
- **DTOs in DocQA.Shared** — never expose EF models directly over the API
- **Secrets via User Secrets (dev) / env vars (prod)** — never commit API keys; use `dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."`
- **C# only** — no JavaScript; Blazor WASM handles all UI
- **Scoped conventions** — read the matching file in `.github/instructions/` (`server`, `client`, `shared`, `playwright`) before editing that project

---

## API Endpoints

```
POST   /api/documents                    Upload document (multipart/form-data, field: file)
GET    /api/documents                    List all documents
GET    /api/documents/{id}               Get document metadata + content
DELETE /api/documents/{id}              Delete document and messages

POST   /api/documents/{id}/query         Ask a question → { answer, excerpt, documentId, messageId }
GET    /api/documents/{id}/messages      Get conversation history

GET    /api/system/info                  Server build version + active Claude model (for About page)
```

---

## Claude Integration

- `ClaudeService` owns all logic — prompt construction, truncation, response parsing, exception classification — and never touches the network
- `AnthropicMessageSender` is the **only** class that calls `Anthropic.SDK`, behind `IMessageSender`. Keep it that way: unit tests mock `IMessageSender`, and acceptance/browser tests swap `IClaudeService` for a stub
- Truncate document content to **150,000 characters** before sending
- System prompt: answer from document only; append an "Excerpt:" after the answer
- Max response tokens: **1024**; streaming disabled

### Query error contract

Claude failures surface as typed exceptions → JSON error payload → typed client exception. A new failure mode must be added in all three places.

| Exception (`Services/`) | `QueryErrorResponse.Code` | HTTP |
|---|---|---|
| `ClaudeAuthenticationException` | `CLAUDE_AUTH_FAILED` | 502 |
| `ClaudeUnavailableException` | `CLAUDE_UNAVAILABLE` | 503 |
| *(anything else)* | `QUERY_PROCESSING_FAILED` | 500 |

`ApiClient.QueryDocumentAsync` reads `Code` back and throws `ApiException` so the UI can distinguish server-unreachable from Claude-unreachable from bad-API-key.

---

## PDF Parsing

Use `PdfPig` (`UglyToad.PdfPig`) for PDF text extraction — pure .NET, no native dependencies.

```csharp
using UglyToad.PdfPig;

public static string ExtractText(Stream pdfStream)
{
    using var document = PdfDocument.Open(pdfStream);
    var sb = new StringBuilder();
    foreach (var page in document.GetPages())
        sb.AppendLine(page.Text);
    return sb.ToString();
}
```

---

## Blazor Pages

**`Index.razor` (`/`)** — document list + upload
- Table: FileName, UploadedAt, FileSizeBytes, [Open] [Delete] buttons
- File input (`accept=".txt,.pdf"`), upload button, progress indicator
- On upload success: add to list, clear input

**`DocumentChat.razor` (`/document/{Id:int}`)** — chat interface
- Header: document name + back link
- Scrollable message bubble list (user/assistant)
- Text box + [Ask] button; append user message immediately, then assistant reply on response
- Empty state: "Ask a question about this document to get started."

**`About.razor` (`/about`)** — client `BuildVersion` from `wwwroot/appsettings.json` plus server build version and active Claude model from `GET /api/system/info`

`Counter.razor` and `Weather.razor` are unused template leftovers.

---

## Testing

```bash
dotnet test DocQA.Tests.Unit
dotnet test DocQA.Tests.Acceptance
dotnet test DocQA.Tests.Browser
dotnet test DocQA.Tests.Unit --filter "FullyQualifiedName~ClaudeServiceTests"
```

- `Program.cs` ends with `public partial class Program { }` so `WebApplicationFactory<Program>` can bind it — do not remove
- Browser tests need Playwright browsers installed first: `pwsh DocQA.Tests.Browser/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium`
- `BrowserFixture` shells out to `dotnet publish DocQA.Client` and serves the published `wwwroot` — slow but exact. See `.github/instructions/playwright.instructions.md` before changing it
- Browser tests currently run in the Gitea pipeline only, not in GitHub Actions

---

## Entity Framework

- `AppDbContext` has `DbSet<Document>` and `DbSet<Message>`
- `Message` → `Document` FK with `OnDelete(DeleteBehavior.Cascade)`. `DocumentService.DeleteAsync` uses `ExecuteDeleteAsync`, which bypasses the change tracker — message cleanup depends on the DB-level cascade
- Use `AsNoTracking()` for read-only queries
- Migrations live in `DocQA.Server/Data/Migrations/`
- `Program.cs` runs `db.Database.Migrate()` at startup, so a schema change without a committed migration breaks production
- Migration commands:
  ```bash
  dotnet ef migrations add <Name> --project DocQA.Server
  dotnet ef database update --project DocQA.Server
  ```

---

## Azure Deployment

App Service `docqa-server-krisbbb` (API) + Static Web App (Blazor WASM). Migrations apply automatically at startup.

App Service environment variables:

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | `DataSource=/home/docqa.db` |
| `Anthropic__ApiKey` | Anthropic API key |
| `Anthropic__Model` | model name (optional override) |

GitHub Actions secrets required:
- `AZURE_WEBAPP_PUBLISH_PROFILE` — App Service publish profile
- `AZURE_STATIC_WEB_APPS_API_TOKEN` — Static Web App token

The API key is set as an App Service env var in the portal, never in Actions.

### Client configuration

`DocQA.Client/wwwroot/appsettings.json` holds `ApiBaseUrl` and `BuildVersion`, and is **committed pointing at the deployed Azure API**. Running the client against a local server means temporarily changing `ApiBaseUrl` to `https://localhost:7221` — do not commit that change.

CORS is allow-listed in `Program.cs`: explicit origins plus any `*.azurestaticapps.net` or `localhost` host.

---

## Out of Scope (v1)

- User authentication
- File formats beyond `.txt` and `.pdf`
- Streaming responses
- Multi-document queries
