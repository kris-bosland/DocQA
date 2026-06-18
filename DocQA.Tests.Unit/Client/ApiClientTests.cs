using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using DocQA.Client.Services;
using DocQA.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Moq;

namespace DocQA.Tests.Unit.Client;

public class ApiClientTests
{
    [Fact]
    public async Task GetDocumentsAsync_ReturnsParsedList()
    {
        var docs = new List<DocumentDto>
        {
            new() { Id = 1, FileName = "a.txt", FileSizeBytes = 10, UploadedAt = DateTime.UtcNow }
        };

        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, docs));
        var client = CreateApiClient(handler);

        var result = await client.GetDocumentsAsync();

        Assert.Single(result);
        Assert.Equal("a.txt", result[0].FileName);
    }

    [Fact]
    public async Task TryGetDocumentAsync_NotFound_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateApiClient(handler);

        var result = await client.TryGetDocumentAsync(123);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetDocumentAsync_ServerError_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateApiClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.TryGetDocumentAsync(5));
    }

    [Fact]
    public async Task QueryDocumentAsync_BadRequest_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = CreateApiClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.QueryDocumentAsync(1, ""));
    }

    [Fact]
    public async Task UploadDocumentAsync_UsesMultipartFieldNamedFile()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var doc = new DocumentDto
        {
            Id = 7,
            FileName = "upload.txt",
            FileSizeBytes = 12,
            UploadedAt = DateTime.UtcNow
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.OK, doc);
        });
        var client = CreateApiClient(handler);

        var file = new Mock<IBrowserFile>();
        file.SetupGet(f => f.Name).Returns("upload.txt");
        file.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("hello")));

        var result = await client.UploadDocumentAsync(file.Object);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);

        Assert.NotNull(captured);
        Assert.NotNull(capturedBody);
        Assert.Contains("name=file", capturedBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upload.txt", capturedBody, StringComparison.OrdinalIgnoreCase);
    }

    private static ApiClient CreateApiClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:7221/")
        };
        return new ApiClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(payload)
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
