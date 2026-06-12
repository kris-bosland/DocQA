using DocQA.Server.Services;

namespace DocQA.Tests.Acceptance.Fixtures;

public class StubClaudeService : IClaudeService
{
    public Task<(string Answer, string Excerpt)> QueryDocumentAsync(
        string content, string question, CancellationToken ct = default)
        => Task.FromResult(("stub answer", "stub excerpt"));
}
