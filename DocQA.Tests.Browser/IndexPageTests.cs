using System.Text;
using DocQA.Tests.Browser.Fixtures;
using Microsoft.Playwright;

namespace DocQA.Tests.Browser;

[Collection(BrowserCollection.Name)]
public class IndexPageTests(BrowserFixture fixture)
{
    [Fact]
    public async Task PageLoads_AndTableIsVisible()
    {
        var page = await OpenIndexPageAsync(fixture);

        await page.Locator("table").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("table").IsVisibleAsync());
    }

    [Fact]
    public async Task UploadTxt_AddsRow_AndDeleteRemovesIt()
    {
        var page = await OpenIndexPageAsync(fixture);
        var fileName = $"browser-{Guid.NewGuid():N}.txt";
        var filePath = await CreateTempTextFileAsync(fileName, "browser upload test");

        try
        {
            await UploadFileAsync(page, filePath);

            var row = page.Locator($"tbody tr:has-text(\"{fileName}\")");
            await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.True(await row.IsVisibleAsync());

            await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
            await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
            Assert.Equal(0, await row.CountAsync());
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task UploadPdf_AddsRow()
    {
        var page = await OpenIndexPageAsync(fixture);
        var pdfPath = FindSamplePdfPath();

        await UploadFileAsync(page, pdfPath);

        var row = page.Locator("tbody tr:has-text(\"sample.pdf\")");
        await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await row.IsVisibleAsync());
    }

    private static async Task<IPage> OpenIndexPageAsync(BrowserFixture fixture)
    {
        var page = await fixture.Browser.NewPageAsync();

        await page.GotoAsync(fixture.AppBaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        await page.Locator("table").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("table").IsVisibleAsync());
        return page;
    }

    private static async Task UploadFileAsync(IPage page, string filePath)
    {
        await page.Locator("input[type='file']").SetInputFilesAsync(filePath);
        await page.GetByRole(AriaRole.Button, new() { Name = "Upload" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private static async Task<string> CreateTempTextFileAsync(string fileName, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        return path;
    }

    private static string FindSamplePdfPath()
    {
        var solutionRoot = FindSolutionRoot();
        var samplePath = Path.Combine(solutionRoot, "DocQA.Tests.Unit", "TestData", "sample.pdf");
        if (!File.Exists(samplePath))
            throw new FileNotFoundException("Could not find sample.pdf.", samplePath);
        return samplePath;
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 4 && directory is not null; i++)
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate solution root.");
    }
}