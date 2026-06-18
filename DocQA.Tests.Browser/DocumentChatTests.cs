using DocQA.Tests.Browser.Fixtures;
using Microsoft.Playwright;

namespace DocQA.Tests.Browser;

[Collection(BrowserCollection.Name)]
public class DocumentChatTests(BrowserFixture fixture)
{
    [Fact]
    public async Task OpenDocument_NavigatesToChatAndShowsEmptyState()
    {
        var page = await fixture.Browser.NewPageAsync();
        var fileName = $"chat-{Guid.NewGuid():N}.txt";
        var filePath = await CreateTempTextFileAsync(fileName, "This is a chat test document.");

        try
        {
            await page.GotoAsync(fixture.AppBaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.Locator("input[type='file']").SetInputFilesAsync(filePath);
            await page.GetByRole(AriaRole.Button, new() { Name = "Upload" }).ClickAsync();

            var row = page.Locator($"tbody tr:has-text(\"{fileName}\")");
            await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.True(await row.IsVisibleAsync());

            await row.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync();
            await page.WaitForURLAsync("**/document/*");

            var heading = page.GetByRole(AriaRole.Heading, new() { Name = fileName });
            await heading.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.True(await heading.IsVisibleAsync());

            var emptyState = page.GetByText("Ask a question about this document to get started.");
            await emptyState.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.True(await emptyState.IsVisibleAsync());
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AskingQuestion_ShowsUserBubble_AndAssistantReply()
    {
        var page = await fixture.Browser.NewPageAsync();
        var fileName = $"chat-answer-{Guid.NewGuid():N}.txt";
        var filePath = await CreateTempTextFileAsync(fileName, "The answer is hidden in here.");
        var question = "What is the answer?";

        try
        {
            await page.GotoAsync(fixture.AppBaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.Locator("input[type='file']").SetInputFilesAsync(filePath);
            await page.GetByRole(AriaRole.Button, new() { Name = "Upload" }).ClickAsync();

            var row = page.Locator($"tbody tr:has-text(\"{fileName}\")");
            await row.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync();
            await page.WaitForURLAsync("**/document/*");

            await page.GetByRole(AriaRole.Textbox).FillAsync(question);
            await page.GetByRole(AriaRole.Button, new() { Name = "Ask" }).ClickAsync();

            var userBubble = page.GetByText(question);
            await userBubble.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.True(await userBubble.IsVisibleAsync());

            var assistantBubble = page.GetByText("stub answer");
            await assistantBubble.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.True(await assistantBubble.IsVisibleAsync());
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private static async Task<string> CreateTempTextFileAsync(string fileName, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}