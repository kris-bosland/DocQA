# DocQA

A web app to allow a user to upload documents and ask an AI questions about it.

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

## Client API Base URL

The Blazor client reads `ApiBaseUrl` from `DocQA.Client/wwwroot/appsettings.json`.

Default (local development):
- `https://localhost:7221`

Important:
- This value is for local development only.
- For deployment, set `ApiBaseUrl` to the deployed API URL (for example, your Azure App Service URL).
