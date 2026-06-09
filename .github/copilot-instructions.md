# DocQA — Copilot Instructions

Document Q&A tool. Users upload `.txt`/`.pdf` files, then ask natural-language questions answered by Claude AI with a cited excerpt.

Full specification: [docs/docqa_project_spec.md](../docs/docqa_project_spec.md)

---

## Tech Stack

| Layer | Technology |
|---|---|
| Front-end | Blazor WebAssembly (hosted, C# only — no JS frameworks) |
| API | ASP.NET Core Minimal API (.NET 9) — no MVC controllers |
| AI | Anthropic Claude API — `claude-3-5-haiku-20241022` via `Anthropic.SDK` NuGet |
| ORM | Entity Framework Core 9 (code-first, migrations) |
| Database (dev) | SQL Server LocalDB |
| Database (prod) | Azure SQL Basic tier |
| PDF parsing | PdfPig (NuGet, pure .NET) |
| Hosting | Azure App Service (API) + Azure Static Web Apps (Blazor WASM) |
| CI/CD | GitHub Actions |

---

## Solution Structure

```
DocQA/
├── DocQA.sln
├── DocQA.Server/           ← ASP.NET Core host + Minimal API
│   ├── Endpoints/          ← DocumentEndpoints.cs, QueryEndpoints.cs
│   ├── Services/           ← IDocumentService, DocumentService, IClaudeService, ClaudeService
│   ├── Data/               ← AppDbContext, Migrations/
│   └── Models/             ← Document.cs, Message.cs
├── DocQA.Client/           ← Blazor WebAssembly
│   ├── Pages/              ← Index.razor (list/upload), DocumentChat.razor
│   ├── Services/           ← ApiClient.cs (typed HttpClient wrapper)
│   └── Shared/             ← MainLayout.razor
└── DocQA.Shared/           ← DTOs: DocumentDto, UploadDocumentRequest, QueryRequest, QueryResponse
```

---

## Coding Conventions

- **Minimal API only** — register endpoints as extension methods in `Endpoints/`, no controllers
- **Interfaces for services** — `IDocumentService` / `IClaudeService` for testability
- **Async throughout** — all I/O methods are `async Task<T>` with `CancellationToken`
- **DTOs in DocQA.Shared** — never expose EF models directly over the API
- **Secrets via User Secrets (dev) / env vars (prod)** — never commit API keys; use `dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."`
- **C# only** — no JavaScript; Blazor WASM handles all UI

---

## API Endpoints

```
POST   /api/documents                    Upload document (multipart/form-data, field: file)
GET    /api/documents                    List all documents
GET    /api/documents/{id}               Get document metadata + content
DELETE /api/documents/{id}              Delete document and messages

POST   /api/documents/{id}/query         Ask a question → { answer, excerpt, documentId, messageId }
GET    /api/documents/{id}/messages      Get conversation history
```

---

## Claude Integration

- Truncate document content to **150,000 characters** before sending (Claude haiku supports 200K tokens)
- System prompt: answer from document only; append an "Excerpt:" after the answer
- Max response tokens: **1024**

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

---

## Entity Framework

- `AppDbContext` has `DbSet<Document>` and `DbSet<Message>`
- `Message` → `Document` FK with `OnDelete(DeleteBehavior.Cascade)`
- Use `AsNoTracking()` for read-only queries
- Migration commands:
  ```bash
  dotnet ef migrations add InitialCreate --project DocQA.Server
  dotnet ef database update --project DocQA.Server
  ```

---

## Azure Deployment

Resources: `docqa-rg` resource group, Azure SQL Basic (or SQLite), App Service F1 (API), Static Web App free (Blazor WASM).

GitHub Actions secrets required:
- `AZURE_WEBAPP_PUBLISH_PROFILE` — App Service publish profile
- `AZURE_STATIC_WEB_APPS_API_TOKEN` — Static Web App token
- `ANTHROPIC_API_KEY` — set as App Service env var in portal (not in Actions)

---

## Out of Scope (v1)

- User authentication
- File formats beyond `.txt` and `.pdf`
- Streaming responses
- Multi-document queries
