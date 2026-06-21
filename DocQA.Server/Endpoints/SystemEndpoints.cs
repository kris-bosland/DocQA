using DocQA.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocQA.Server.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/info", GetSystemInfo);
        return app;
    }

    private static Ok<SystemInfoDto> GetSystemInfo(IConfiguration configuration)
    {
        var serverBuildVersion = configuration["Server:BuildVersion"] ?? "not configured";
        var anthropicModel = configuration["Anthropic:Model"] ?? "claude-haiku-4-5";

        return TypedResults.Ok(new SystemInfoDto
        {
            ServerBuildVersion = serverBuildVersion,
            AnthropicModel = anthropicModel
        });
    }
}