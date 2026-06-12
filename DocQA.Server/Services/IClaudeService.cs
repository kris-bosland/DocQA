namespace DocQA.Server.Services;

public interface IClaudeService
{
    Task<(string Answer, string Excerpt)> QueryDocumentAsync(
        string content, string question, CancellationToken ct = default);
}
