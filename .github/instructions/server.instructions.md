---
applyTo: "DocQA.Server/**"
---

# Server-Side Conventions

## Endpoints
- Register all endpoints as extension methods on `IEndpointRouteBuilder` in `Endpoints/`
- Call `app.MapDocumentEndpoints()` and `app.MapQueryEndpoints()` from `Program.cs`
- Return `Results<T1, T2>` union types for endpoints with multiple outcomes

## Services
- Define an interface (`IDocumentService`, `IClaudeService`) before the implementation
- Register services as scoped unless they are stateless utilities (then singleton)
- Inject `ILogger<T>` for all services

## EF Core
- Never return `Document` or `Message` EF models from endpoints — map to DTOs first
- Migrations go in `DocQA.Server/Data/Migrations/`
- Use `AsNoTracking()` for read-only queries
- `AppDbContext` exposes `DbSet<Document>` and `DbSet<Message>`; access via expression-body properties (`=> Set<T>()`)
- Configure `Message` → `Document` FK with `OnDelete(DeleteBehavior.Cascade)` in `OnModelCreating`
- Run migrations with: `dotnet ef migrations add <Name> --project DocQA.Server`
- Always assign `DateTime.UtcNow` for `UploadedAt`/`CreatedAt`; never `DateTime.Now`

## Error handling
- Return `404 NotFound` when an entity is not found by ID
- Return `400 BadRequest` with a message string for invalid input
- Let unhandled exceptions bubble up to the global handler (configured in `Program.cs`)
