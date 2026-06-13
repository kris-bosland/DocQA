namespace DocQA.Shared;

public record DocumentDto
{
    public required int Id { get; init; }
    public required string FileName { get; init; }
    public required long FileSizeBytes { get; init; }
    public required DateTime UploadedAt { get; init; }
    public string? Content { get; init; }
}
