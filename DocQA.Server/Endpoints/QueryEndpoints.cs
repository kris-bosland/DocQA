using DocQA.Server.Data;
using DocQA.Server.Models;
using DocQA.Server.Services;
using DocQA.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace DocQA.Server.Endpoints;

public static class QueryEndpoints
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents/{id:int}/query", QueryDocument).DisableAntiforgery();
        app.MapGet("/api/documents/{id:int}/messages", GetMessages);
        return app;
    }

    private static async Task<Results<Ok<QueryResponse>, NotFound>> QueryDocument(
        int id,
        QueryRequest request,
        IDocumentService documentService,
        IClaudeService claudeService,
        AppDbContext db,
        CancellationToken ct)
    {
        var document = await documentService.GetByIdAsync(id, ct);
        if (document is null)
            return TypedResults.NotFound();

        var (answer, excerpt) = await claudeService.QueryDocumentAsync(
            document.Content ?? string.Empty, request.Question, ct);

        var userMsg = new Message
        {
            DocumentId = id,
            Role = "user",
            Content = request.Question,
            CreatedAt = DateTime.UtcNow
        };
        var assistantMsg = new Message
        {
            DocumentId = id,
            Role = "assistant",
            Content = answer,
            CreatedAt = DateTime.UtcNow
        };
        db.Messages.AddRange(userMsg, assistantMsg);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new QueryResponse
        {
            Answer = answer,
            Excerpt = excerpt,
            DocumentId = id,
            MessageId = assistantMsg.Id
        });
    }

    private static async Task<Results<Ok<IEnumerable<MessageDto>>, NotFound>> GetMessages(
        int id,
        AppDbContext db,
        CancellationToken ct)
    {
        var exists = await db.Documents.AnyAsync(d => d.Id == id, ct);
        if (!exists)
            return TypedResults.NotFound();

        var messages = await db.Messages
            .AsNoTracking()
            .Where(m => m.DocumentId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        return TypedResults.Ok(messages.AsEnumerable());
    }
}
