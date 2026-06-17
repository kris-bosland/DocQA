# DocQA

A web app to allow a user to upload documents and ask an AI questions about it.

## Client API Base URL

The Blazor client reads `ApiBaseUrl` from `DocQA.Client/wwwroot/appsettings.json`.

Default (local development):
- `https://localhost:7221`

Important:
- This value is for local development only.
- For deployment, set `ApiBaseUrl` to the deployed API URL (for example, your Azure App Service URL).
