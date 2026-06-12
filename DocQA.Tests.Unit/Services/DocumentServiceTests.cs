using Microsoft.EntityFrameworkCore;
using DocQA.Server.Data;
using DocQA.Server.Models;

namespace DocQA.Tests.Unit.Services;

public class DocumentServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task DeleteDocument_CascadesMessages()
    {
        await using var ctx = CreateContext();

        var doc = new Document
        {
            FileName = "test.txt",
            Content = "hello",
            FileSizeBytes = 5,
            UploadedAt = DateTime.UtcNow
        };
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        ctx.Messages.Add(new Message
        {
            DocumentId = doc.Id,
            Role = "user",
            Content = "question",
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // Include messages so EF change tracker cascades the delete
        var docWithMessages = await ctx.Documents
            .Include(d => d.Messages)
            .SingleAsync(d => d.Id == doc.Id);

        ctx.Documents.Remove(docWithMessages);
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await ctx.Messages.CountAsync());
    }

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    public async Task Message_AcceptsValidRoles(string role)
    {
        await using var ctx = CreateContext();

        var doc = new Document
        {
            FileName = "f.txt",
            Content = "x",
            FileSizeBytes = 1,
            UploadedAt = DateTime.UtcNow
        };
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var msg = new Message
        {
            DocumentId = doc.Id,
            Role = role,
            Content = "content",
            CreatedAt = DateTime.UtcNow
        };
        ctx.Messages.Add(msg);
        await ctx.SaveChangesAsync();

        var saved = await ctx.Messages.FindAsync(msg.Id);
        Assert.Equal(role, saved!.Role);
    }
}
