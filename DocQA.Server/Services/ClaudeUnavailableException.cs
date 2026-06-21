namespace DocQA.Server.Services;

public sealed class ClaudeUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
}
