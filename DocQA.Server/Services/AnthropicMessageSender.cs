using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace DocQA.Server.Services;

public sealed class AnthropicMessageSender(AnthropicClient client) : IMessageSender
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
        return result.Message.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
    }
}
