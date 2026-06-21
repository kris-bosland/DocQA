namespace DocQA.Shared;

public record SystemInfoDto
{
    public required string ServerBuildVersion { get; init; }
    public required string AnthropicModel { get; init; }
}