using DocQA.Server.Services;
using DocQA.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocQA.Server.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents", UploadDocument).DisableAntiforgery();
        app.MapGet("/api/documents", GetAll);
        app.MapGet("/api/documents/{id:int}", GetById);
        app.MapDelete("/api/documents/{id:int}", Delete);
        return app;
    }

    private static async Task<Results<BadRequest<string>, Created<DocumentDto>>> UploadDocument(
        IFormFile? file, IDocumentService service, CancellationToken ct)
    {
        if (file is null)
            return TypedResults.BadRequest("No file provided.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".txt" && ext != ".pdf")
            return TypedResults.BadRequest("Only .txt and .pdf files are supported.");

        const long MaxBytes = 10 * 1024 * 1024;
        if (file.Length > MaxBytes)
            return TypedResults.BadRequest("File exceeds the 10 MB limit.");

        try
        {
            var doc = await service.CreateAsync(file, ct);
            return TypedResults.Created($"/api/documents/{doc.Id}", doc);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Ok<IEnumerable<DocumentDto>>> GetAll(
        IDocumentService service, CancellationToken ct)
    {
        var docs = await service.GetAllAsync(ct);
        return TypedResults.Ok(docs);
    }

    private static async Task<Results<Ok<DocumentDto>, NotFound>> GetById(
        int id, IDocumentService service, CancellationToken ct)
    {
        var doc = await service.GetByIdAsync(id, ct);
        return doc is null ? TypedResults.NotFound() : TypedResults.Ok(doc);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        int id, IDocumentService service, CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
