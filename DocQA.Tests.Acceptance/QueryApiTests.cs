using System.Net;
using System.Net.Http.Json;
using System.Text;
using DocQA.Shared;
using DocQA.Tests.Acceptance.Fixtures;

namespace DocQA.Tests.Acceptance;

public class QueryApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task QueryDocument_UnknownDocument_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/documents/9999/query",
            new QueryRequest { Question = "What is this?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task QueryDocument_ValidDocument_ReturnsQueryResponse()
    {
        var docId = await UploadTextDocumentAsync("The quick brown fox.");

        var response = await _client.PostAsJsonAsync(
            $"/api/documents/{docId}/query",
            new QueryRequest { Question = "What animal is mentioned?" });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<QueryResponse>();
        Assert.NotNull(result);
        Assert.Equal("stub answer", result.Answer);
        Assert.Equal("stub excerpt", result.Excerpt);
        Assert.Equal(docId, result.DocumentId);
        Assert.True(result.MessageId > 0);
    }

    [Fact]
    public async Task GetMessages_AfterQuery_ReturnsUserAndAssistantMessages()
    {
        var docId = await UploadTextDocumentAsync("Some document content.");

        await _client.PostAsJsonAsync(
            $"/api/documents/{docId}/query",
            new QueryRequest { Question = "What does the document say?" });

        var response = await _client.GetAsync($"/api/documents/{docId}/messages");
        response.EnsureSuccessStatusCode();

        // Check that messages are ordered by CreatedAt, not Id.
        var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
        Assert.NotNull(messages);
        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("What does the document say?", messages[0].Content);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("stub answer", messages[1].Content);
    }

    [Fact]
    public async Task GetMessages_AfterQuery_PersistsAcrossRequests()
    {
        var docId = await UploadTextDocumentAsync("Some document content.");

        await _client.PostAsJsonAsync(
            $"/api/documents/{docId}/query",
            new QueryRequest { Question = "What does the document say?" });

        // Call twice to ensure messages are persisted (GET again -> same count).
        var firstResponse = await _client.GetAsync($"/api/documents/{docId}/messages");
        var secondResponse = await _client.GetAsync($"/api/documents/{docId}/messages");
        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();

        var firstMessages = await firstResponse.Content.ReadFromJsonAsync<List<MessageDto>>();
        var secondMessages = await secondResponse.Content.ReadFromJsonAsync<List<MessageDto>>();
        Assert.NotNull(firstMessages);
        Assert.Equal(2, firstMessages.Count);
        Assert.NotNull(secondMessages);
        Assert.Equal(2, secondMessages.Count);
        Assert.Equal(firstMessages[0].Id, secondMessages[0].Id);
        Assert.Equal(firstMessages[1].Id, secondMessages[1].Id);
    }

    [Fact]
    public async Task GetMessages_UnknownDocument_Returns404()
    {
        var response = await _client.GetAsync("/api/documents/9999/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> UploadTextDocumentAsync(string content)
    {
        var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(bytes, "file", "test.txt");

        var response = await _client.PostAsync("/api/documents", form);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<DocumentDto>();
        return doc!.Id;
    }
}
