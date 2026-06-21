namespace DocQA.Shared;

public record QueryResponse
{
    public required string Answer { get; init; }
    public required string Excerpt { get; init; }
    public required int DocumentId { get; init; }
    public required int MessageId { get; init; }
}

public record QueryErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
    public int StatusCode { get; init; }
}
