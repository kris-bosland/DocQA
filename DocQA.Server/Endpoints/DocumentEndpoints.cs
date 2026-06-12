using DocQA.Server.Services;

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

    private static async Task<IResult> UploadDocument(
        IFormFile? file, IDocumentService service, CancellationToken ct)
    {
        if (file is null)
            return Results.BadRequest("No file provided.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".txt" && ext != ".pdf")
            return Results.BadRequest("Only .txt and .pdf files are supported.");

        var doc = await service.CreateAsync(file, ct);
        return Results.Created($"/api/documents/{doc.Id}", doc);
    }

    private static async Task<IResult> GetAll(IDocumentService service, CancellationToken ct)
    {
        var docs = await service.GetAllAsync(ct);
        return Results.Ok(docs);
    }

    private static async Task<IResult> GetById(int id, IDocumentService service, CancellationToken ct)
    {
        var doc = await service.GetByIdAsync(id, ct);
        return doc is null ? Results.NotFound() : Results.Ok(doc);
    }

    private static async Task<IResult> Delete(int id, IDocumentService service, CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
