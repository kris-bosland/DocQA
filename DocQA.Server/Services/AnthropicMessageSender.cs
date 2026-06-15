using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace DocQA.Server.Services;

public sealed class AnthropicMessageSender(AnthropicClient client, ILogger<AnthropicMessageSender> logger) : IMessageSender
{
    public async Task<string> SendAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var parameters = new MessageParameters
        {
            Messages = [new Message(RoleType.User, userMessage)],
            System = [new SystemMessage(systemPrompt)],
            Model = "claude-3-5-haiku-20241022",
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
