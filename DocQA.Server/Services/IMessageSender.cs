namespace DocQA.Server.Services;

public interface IMessageSender
{
    Task<string> SendAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
