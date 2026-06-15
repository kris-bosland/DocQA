namespace DocQA.Shared;

public record QueryRequest
{
    public required string Question { get; init; }
}
