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

    private static async Task<Results<Ok<QueryResponse>, NotFound, BadRequest<string>, JsonHttpResult<QueryErrorResponse>>> QueryDocument(
        int id,
        QueryRequest request,
        IDocumentService documentService,
        IClaudeService claudeService,
        AppDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return TypedResults.BadRequest("Question cannot be empty.");

        var document = await documentService.GetByIdAsync(id, ct);
        if (document is null)
            return TypedResults.NotFound();

        var logger = loggerFactory.CreateLogger("QueryEndpoints");
        string answer;
        string excerpt;
        try
        {
            (answer, excerpt) = await claudeService.QueryDocumentAsync(
                document.Content ?? string.Empty, request.Question, ct);
        }
        catch (ClaudeAuthenticationException)
        {
            return TypedResults.Json(
                new QueryErrorResponse
                {
                    Code = "CLAUDE_AUTH_FAILED",
                    Message = "The server could not authenticate with Claude API.",
                    Details = "Check the configured API key.",
                    StatusCode = StatusCodes.Status502BadGateway
                },
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (ClaudeUnavailableException)
        {
            return TypedResults.Json(
                new QueryErrorResponse
                {
                    Code = "CLAUDE_UNAVAILABLE",
                    Message = "The server could not reach Claude API.",
                    Details = "Please try again shortly.",
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure while querying Claude for document {DocumentId}.", id);
            return TypedResults.Json(
                new QueryErrorResponse
                {
                    Code = "QUERY_PROCESSING_FAILED",
                    Message = "The server hit an unexpected error while processing your question.",
                    Details = "The API is reachable, but query processing failed on the server. Check server logs.",
                    StatusCode = StatusCodes.Status500InternalServerError
                },
                statusCode: StatusCodes.Status500InternalServerError);
        }

        //Control the time of message creation to ensure the user message is always CreatedAt before the assistant message.
        var now = DateTime.UtcNow;
        var userMsg = new Message
        {
            DocumentId = id,
            Role = "user",
            Content = request.Question,
            CreatedAt = now
        };
        var assistantMsg = new Message
        {
            DocumentId = id,
            Role = "assistant",
            Content = answer,
            CreatedAt = now.AddMilliseconds(1)
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
