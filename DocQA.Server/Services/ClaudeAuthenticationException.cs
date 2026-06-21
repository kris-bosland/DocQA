namespace DocQA.Server.Services;

public sealed class ClaudeAuthenticationException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
}