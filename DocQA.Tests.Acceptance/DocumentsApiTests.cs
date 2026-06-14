using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DocQA.Shared;
using DocQA.Tests.Acceptance.Fixtures;

namespace DocQA.Tests.Acceptance;

public class DocumentsApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static MultipartFormDataContent BuildFileUpload(
        string content = "hello world",
        string fileName = "test.txt")
    {
        var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        bytes.Headers.ContentType = new MediaTypeHeaderValue(
            Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                _      => "application/octet-stream"
            });
        form.Add(bytes, "file", fileName);
        return form;
    }

    [Fact]
    public async Task PostDocument_WithTxt_Returns201WithId()
    {
        var response = await _client.PostAsync("/api/documents", BuildFileUpload());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);
        Assert.NotNull(doc);
        Assert.True(doc.Id > 0);
        Assert.Equal("test.txt", doc.FileName);
    }

    [Fact]
    public async Task GetDocuments_ReturnsUploadedFile()
    {
        var fileName = $"list-{Guid.NewGuid():N}.txt";
        await _client.PostAsync("/api/documents", BuildFileUpload(fileName: fileName));

        var response = await _client.GetAsync("/api/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var docs = await response.Content.ReadFromJsonAsync<List<DocumentDto>>(JsonOpts);
        Assert.NotNull(docs);
        Assert.Contains(docs, d => d.FileName == fileName);
    }

    [Fact]
    public async Task GetDocumentById_ReturnsMetadataAndContent()
    {
        const string fileContent = "retrievable content";
        var upload = await _client.PostAsync("/api/documents",
            BuildFileUpload(content: fileContent));
        var created = await upload.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);

        var response = await _client.GetAsync($"/api/documents/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);
        Assert.NotNull(doc);
        Assert.Equal(created.Id, doc.Id);
        Assert.Equal(fileContent, doc.Content);
    }

    [Fact]
    public async Task DeleteDocument_Returns204_ThenGetReturns404()
    {
        var upload = await _client.PostAsync("/api/documents",
            BuildFileUpload(fileName: "to-delete.txt"));
        var created = await upload.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);

        var deleteResponse = await _client.DeleteAsync($"/api/documents/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/documents/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostDocument_WithNoFile_Returns400()
    {
        var response = await _client.PostAsync("/api/documents",
            new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDocument_WithInvalidExtension_Returns400()
    {
        var response = await _client.PostAsync("/api/documents",
            BuildFileUpload(fileName: "malware.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDocument_WithPdf_Returns201WithNonEmptyContent()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var pdfStream = asm.GetManifestResourceStream("DocQA.Tests.Acceptance.TestData.sample.pdf")
            ?? throw new InvalidOperationException("Embedded resource TestData/sample.pdf not found.");
        var form = new MultipartFormDataContent();
        var pdfBytes = new StreamContent(pdfStream);
        pdfBytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdfBytes, "file", "sample.pdf");

        var response = await _client.PostAsync("/api/documents", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);
        Assert.NotNull(doc);
        Assert.False(string.IsNullOrWhiteSpace(doc.Content));
    }
}
