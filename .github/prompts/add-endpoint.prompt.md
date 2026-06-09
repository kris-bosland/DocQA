---
mode: agent
description: Scaffold a new Minimal API endpoint pair (endpoint class + service method)
---

Add a new Minimal API endpoint to DocQA.Server.

Steps:
1. Add the route handler to the appropriate file in `DocQA.Server/Endpoints/` (create a new file if needed)
2. Add the corresponding method signature to the service interface in `DocQA.Server/Services/`
3. Implement the method in the service class
4. If new request/response shapes are needed, add DTOs to `DocQA.Shared/`
5. Register the endpoint group in `Program.cs` if it's a new file

Follow the conventions in `.github/instructions/server.instructions.md`.

Endpoint to add: [DESCRIBE THE ENDPOINT HERE]
