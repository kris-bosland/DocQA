# DocQA

A document Q&A tool. Upload `.txt` or `.pdf` files, then ask natural-language questions answered by Claude AI with a cited excerpt from the document.

**Live demo**: [https://witty-ground-04822531e.7.azurestaticapps.net/](https://witty-ground-04822531e.7.azurestaticapps.net/)

## Overview

DocQA is a full-stack application built with:

| Layer | Technology |
|---|---|
| Front-end | Blazor WebAssembly (.NET 10, C# only) |
| API | ASP.NET Core Minimal API (.NET 10) |
| AI | Anthropic Claude (configurable model, default `claude-haiku-4-5`) |
| Database | SQLite (EF Core 10, code-first migrations) |
| PDF parsing | PdfPig (pure .NET) |
| Hosting | Azure App Service (API) + Azure Static Web Apps (client) |
| CI/CD | GitHub Actions (build, test, deploy on push to `main`) |

**Features**:
- Upload `.txt` and `.pdf` documents (up to 10 MB)
- Ask questions about any uploaded document
- Answers include a relevant excerpt from the source document
- Conversation history is persisted per document
- About page shows live client build version, server build version, and active Claude model

**Error handling**:
- Distinguishes between client-to-server connectivity failures, Claude authentication failures, and Claude availability issues
- Server-side query failures return a structured JSON error payload with an explicit error code

## Running Locally

**Prerequisites**: .NET 10 SDK, an Anthropic API key.

1. Set the API key (once per machine):
   ```
   dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project DocQA.Server
   ```
2. Start the API server:
   ```
   dotnet run --project DocQA.Server
   ```
   Runs at `https://localhost:7221`.

3. In a second terminal, start the Blazor client:
   ```
   dotnet run --project DocQA.Client
   ```
   Opens at `https://localhost:7149`.

4. Navigate to `https://localhost:7149` in your browser.

## Configuration

### Client API base URL

The Blazor client reads `ApiBaseUrl` and `BuildVersion` from `DocQA.Client/wwwroot/appsettings.json`.

Default (local development):
```json
{
  "ApiBaseUrl": "https://localhost:7221",
  "BuildVersion": "1.1.0"
}
```

For deployment, set `ApiBaseUrl` to the deployed API URL.

### Server configuration

The server reads from `DocQA.Server/appsettings.json` and environment variable overrides:

| Setting | Key | Default |
|---|---|---|
| Database connection | `ConnectionStrings:DefaultConnection` | `DataSource=docqa.db` |
| Anthropic API key | `Anthropic:ApiKey` | *(required)* |
| Claude model | `Anthropic:Model` | `claude-haiku-4-5` |
| Server build version | `Server:BuildVersion` | `1.1.0` |

For local development, use User Secrets for the API key. Never commit it.

## Deployment Notes

The server uses SQLite in production. Set these App Service environment variables:

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | `DataSource=/home/docqa.db` |
| `Anthropic__ApiKey` | your Anthropic key |
| `Anthropic__Model` | model name (optional, overrides appsettings) |

Database migrations are applied automatically at startup.

### GitHub Actions CI/CD

On push to `main`, the workflow in `.github/workflows/` will:
1. Build and run unit + acceptance tests
2. Publish and deploy the API to Azure App Service
3. Deploy the Blazor client to Azure Static Web Apps

Required GitHub repository secrets:

| Secret | Where to get it |
|---|---|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | App Service → Overview → Download publish profile |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Static Web App → Manage deployment token |

## Solution Structure

```
DocQA/
├── DocQA.Server/           ← ASP.NET Core host + Minimal API
│   ├── Endpoints/          ← DocumentEndpoints, QueryEndpoints, SystemEndpoints
│   ├── Services/           ← ClaudeService, DocumentService, AnthropicMessageSender
│   ├── Data/               ← AppDbContext, Migrations/
│   └── Models/             ← Document, Message
├── DocQA.Client/           ← Blazor WebAssembly
│   ├── Pages/              ← Index.razor, DocumentChat.razor, About.razor
│   ├── Services/           ← ApiClient.cs
│   └── Layout/             ← MainLayout.razor
└── DocQA.Shared/           ← DTOs shared between client and server
```
