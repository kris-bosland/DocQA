using DocQA.Server.Services;

namespace DocQA.Tests.Browser.Fixtures;

public sealed class StubClaudeService : IClaudeService
{
    public Task<(string Answer, string Excerpt)> QueryDocumentAsync(
        string content,
        string question,
        CancellationToken ct = default)
        => Task.FromResult(("stub answer", "stub excerpt"));
}