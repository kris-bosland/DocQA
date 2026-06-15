using DocQA.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocQA.Tests.Unit.Services;

public class ClaudeServiceTests
{
    [Fact]
    public async Task QueryDocumentAsync_SystemPromptContainsExpectedText()
    {
        string? capturedSystemPrompt = null;
        var mockSender = new Mock<IMessageSender>();
        mockSender
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((sp, _, _) => capturedSystemPrompt = sp)
            .ReturnsAsync("The answer.\nExcerpt: relevant text");

        var service = new ClaudeService(mockSender.Object, NullLogger<ClaudeService>.Instance);

        await service.QueryDocumentAsync("document content", "question");

        Assert.NotNull(capturedSystemPrompt);
        Assert.Contains("Answer based only on the document content", capturedSystemPrompt);
    }

    [Fact]
    public async Task QueryDocumentAsync_ContentTruncatedAt150KCharacters()
    {
        string? capturedUserMessage = null;
        var mockSender = new Mock<IMessageSender>();
        mockSender
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, um, _) => capturedUserMessage = um)
            .ReturnsAsync("answer\nExcerpt: excerpt");

        var service = new ClaudeService(mockSender.Object, NullLogger<ClaudeService>.Instance);
        var longContent = new string('x', 160_000);

        await service.QueryDocumentAsync(longContent, "question");

        Assert.NotNull(capturedUserMessage);
        Assert.Contains(new string('x', 150_000), capturedUserMessage);
        Assert.DoesNotContain(new string('x', 150_001), capturedUserMessage);
    }

    [Fact]
    public async Task QueryDocumentAsync_ParsesAnswerAndExcerptFromResponse()
    {
        var mockSender = new Mock<IMessageSender>();
        mockSender
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("The answer text.\nExcerpt: the supporting excerpt");

        var service = new ClaudeService(mockSender.Object, NullLogger<ClaudeService>.Instance);

        var (answer, excerpt) = await service.QueryDocumentAsync("content", "question");

        Assert.Equal("The answer text.", answer);
        Assert.Equal("the supporting excerpt", excerpt);
    }
}
