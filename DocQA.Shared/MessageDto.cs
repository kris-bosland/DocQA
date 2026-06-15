namespace DocQA.Shared;

public record MessageDto
{
    public required int Id { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public required DateTime CreatedAt { get; init; }
}
