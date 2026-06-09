# DocQA — Project Specification
*Document Q&A tool using Blazor + ASP.NET Core + Claude API + Azure*

---

## Purpose

A full-stack portfolio project demonstrating:
- Blazor WebAssembly front-end (C# throughout)
- ASP.NET Core Minimal API backend
- Claude API integration (Anthropic) for document Q&A
- SQL Server / Entity Framework Core data layer
- Azure deployment (App Service + Static Web Apps + Azure SQL)
- GitHub Actions CI/CD pipeline

---

## What It Does

1. User uploads a plain text (.txt) or PDF (.pdf) document
2. Document content is parsed and stored in a database
3. User asks a natural-language question about the document
4. The app sends the question + document content to the Claude API and returns an answer with a relevant excerpt cited
5. Conversation history per document is stored and displayed

---

## What Is Explicitly Out of Scope (v1)

- User authentication / accounts
- File formats beyond .txt and .pdf
- Streaming responses
- Multi-document queries
- Any feature not required for the core upload → ask → answer loop

---

## Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| Front-end | Blazor WebAssembly (hosted) | C# throughout; no JavaScript framework |
| API | ASP.NET Core Minimal API (.NET 9) | Thin, no MVC controllers |
| AI | Anthropic Claude API — `claude-3-5-haiku-20241022` | Cheapest capable model; ~$0.001/call |
| ORM | Entity Framework Core 9 | Code-first, migrations |
| Database (local) | SQL Server LocalDB | Dev environment |
| Database (prod) | Azure SQL (Basic tier, ~$5/mo) | Or use SQLite for zero cost |
| PDF parsing | PdfPig (NuGet) | Pure .NET, no native dependencies |
| Hosting (API) | Azure App Service (Free F1 tier) | Backend |
| Hosting (UI) | Azure Static Web Apps (Free tier) | Blazor WASM |
| CI/CD | GitHub Actions | Deploy to Azure on push to main |
| Secrets (local) | .NET User Secrets (`dotnet user-secrets`) | Never commit API keys |
| Secrets (prod) | Azure App Service Environment Variables | Set in portal or via GitHub Actions secret |

---

## Solution Structure

```
DocQA/                          ← solution root
├── DocQA.sln
├── DocQA.Server/               ← ASP.NET Core host + Minimal API
│   ├── Program.cs
│   ├── Endpoints/
│   │   ├── DocumentEndpoints.cs
│   │   └── QueryEndpoints.cs
│   ├── Services/
│   │   ├── IDocumentService.cs
│   │   ├── DocumentService.cs
│   │   ├── IClaudeService.cs
│   │   └── ClaudeService.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   └── Models/
│       ├── Document.cs
│       └── Message.cs
├── DocQA.Client/               ← Blazor WebAssembly
│   ├── Pages/
│   │   ├── Index.razor          ← document list + upload
│   │   └── DocumentChat.razor   ← chat interface for a document
│   ├── Services/
│   │   └── ApiClient.cs         ← typed HttpClient wrapper
│   └── Shared/
│       └── MainLayout.razor
└── DocQA.Shared/               ← DTOs shared between Client and Server
    ├── DocumentDto.cs
    ├── UploadDocumentRequest.cs
    ├── QueryRequest.cs
    └── QueryResponse.cs
```

---

## Data Model

### Documents table
```
Id              int             PK, identity
FileName        nvarchar(255)   original file name
Content         nvarchar(max)   full parsed text content
UploadedAt      datetime2       UTC
FileSizeBytes   int
```

### Messages table
```
Id              int             PK, identity
DocumentId      int             FK → Documents.Id
Role            nvarchar(20)    "user" or "assistant"
Content         nvarchar(max)   message text
CreatedAt       datetime2       UTC
```

---

## API Endpoints (Minimal API)

### Documents
```
POST   /api/documents           Upload a document (multipart/form-data)
GET    /api/documents           List all documents (Id, FileName, UploadedAt, FileSizeBytes)
GET    /api/documents/{id}      Get document metadata + full content
DELETE /api/documents/{id}      Delete document and all its messages
```

### Queries
```
POST   /api/documents/{id}/query    Ask a question; returns answer + excerpt
GET    /api/documents/{id}/messages Get conversation history for a document
```

### Request / Response shapes (in DocQA.Shared)

**POST /api/documents** — multipart/form-data, field name: `file`

**POST /api/documents/{id}/query**
```json
// Request
{ "question": "What are the permit conditions for stormwater discharge?" }

// Response
{
  "answer": "The permit requires...",
  "excerpt": "Section 4.2: Stormwater discharge shall not exceed...",
  "documentId": 3,
  "messageId": 17
}
```

---

## Claude API Integration

### NuGet package
```
Anthropic.SDK
```

### Prompt strategy

Send the full document content as context. For documents that may exceed the context window (claude-3-5-haiku supports 200K tokens), truncate to ~150K characters with a note. This is a deliberate architectural decision to document in the README.

```csharp
// ClaudeService.cs — approximate structure
public async Task<(string Answer, string Excerpt)> QueryDocumentAsync(
    string documentContent, 
    string question,
    CancellationToken ct = default)
{
    var systemPrompt = """
        You are a document assistant. The user will ask questions about the provided document.
        Answer based only on the document content.
        After your answer, include a brief relevant excerpt from the document prefixed with "Excerpt:".
        If the answer is not in the document, say so clearly.
        """;

    var userMessage = $"""
        Document content:
        ---
        {TruncateIfNeeded(documentContent, maxChars: 150_000)}
        ---
        
        Question: {question}
        """;

    // Call Anthropic.SDK here
    // Model: claude-3-5-haiku-20241022
    // Max tokens: 1024 (sufficient for answer + excerpt)
}
```

### Configuration (appsettings.json structure — values via User Secrets / env vars)
```json
{
  "Anthropic": {
    "ApiKey": ""  // set via: dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
  }
}
```

---

## PDF Parsing

```csharp
// Using PdfPig
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

### Index.razor (/)
- Document list: table with FileName, UploadedAt, FileSizeBytes, [Open] [Delete] buttons
- Upload section: file input (accept=".txt,.pdf"), upload button, progress indicator
- On upload success: add to list, clear input

### DocumentChat.razor (/document/{Id:int})
- Header: document name + link back to list
- Conversation area: scrollable list of user/assistant message bubbles
- Input: text box + [Ask] button
- On submit: POST question, append user message immediately, then append assistant response when it arrives
- Empty state: "Ask a question about this document to get started."

---

## Entity Framework Setup

```csharp
// AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Message>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(m => m.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

```bash
# First migration
dotnet ef migrations add InitialCreate --project DocQA.Server
dotnet ef database update --project DocQA.Server
```

---

## Azure Deployment

### Resources to create (all free or near-free tier)
1. **Resource Group**: `docqa-rg`
2. **Azure SQL Server + Database**: Basic tier (~$5/mo) — or use SQLite to keep cost zero
3. **App Service**: Free F1 tier — hosts `DocQA.Server`
4. **Static Web App**: Free tier — hosts `DocQA.Client` (Blazor WASM)

### GitHub Actions workflow (`.github/workflows/deploy.yml`)
Two jobs:
1. Build + test → publish `DocQA.Server` → deploy to App Service
2. Build + publish `DocQA.Client` → deploy to Static Web App

Secrets needed in GitHub repo settings:
- `AZURE_WEBAPP_PUBLISH_PROFILE` — download from App Service in portal
- `AZURE_STATIC_WEB_APPS_API_TOKEN` — from Static Web App in portal
- `ANTHROPIC_API_KEY` — set as App Service environment variable in portal (not in GitHub Actions)

---
