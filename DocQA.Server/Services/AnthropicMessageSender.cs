using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace DocQA.Server.Services;

public sealed class AnthropicMessageSender(
    AnthropicClient client,
    IConfiguration configuration,
    ILogger<AnthropicMessageSender> logger) : IMessageSender
{
    public async Task<string> SendAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var model = configuration["Anthropic:Model"] ?? "claude-haiku-4-5";

        var parameters = new MessageParameters
        {
            Messages = [new Message(RoleType.User, userMessage)],
            System = [new SystemMessage(systemPrompt)],
            Model = model,
            MaxTokens = 1024,
            Stream = false
        };

        var result = await client.Messages.GetClaudeMessageAsync(parameters, ct);
        var text = result.Message.Content.OfType<TextContent>().FirstOrDefault()?.Text;
        if (string.IsNullOrEmpty(text))
            logger.LogWarning("Anthropic response contained no text content for model {Model}", parameters.Model);
        return text ?? string.Empty;
    }
}
