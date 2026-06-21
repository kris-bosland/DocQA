using System.Net;
using System.Security.Authentication;

namespace DocQA.Server.Services;

public sealed class ClaudeService(IMessageSender messageSender, ILogger<ClaudeService> logger) : IClaudeService
{
    private const int MaxContentChars = 150_000;
    private const string SystemPrompt =
        "Answer based only on the document content provided. " +
        "After your answer, on a new line write \"Excerpt:\" followed by the most relevant " +
        "excerpt from the document that supports your answer.";

    public async Task<(string Answer, string Excerpt)> QueryDocumentAsync(
        string content, string question, CancellationToken ct = default)
    {
        var truncated = content.Length > MaxContentChars ? content[..MaxContentChars] : content;
        var userMessage = $"Document:\n{truncated}\n\nQuestion: {question}";

        logger.LogInformation("Querying Claude for document content of length {Length}", truncated.Length);

        string text;
        try
        {
            text = await messageSender.SendAsync(SystemPrompt, userMessage, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex, "Claude API authentication failed.");
            throw new ClaudeAuthenticationException("Claude API authentication failed.", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogError(ex, "Claude API rejected authentication with status code {StatusCode}.", ex.StatusCode);
            throw new ClaudeAuthenticationException("Claude API rejected authentication.", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reach Claude API while processing a document query.");
            throw new ClaudeUnavailableException("The server could not reach Claude API.", ex);
        }

        return ParseResponse(text);
    }

    private static (string Answer, string Excerpt) ParseResponse(string text)
    {
        var idx = text.LastIndexOf("Excerpt:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return (text.Trim(), string.Empty);

        return (text[..idx].Trim(), text[(idx + "Excerpt:".Length)..].Trim());
    }
}
