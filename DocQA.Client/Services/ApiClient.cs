using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocQA.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace DocQA.Client.Services;

public sealed class ApiException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}

public class ApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<DocumentDto>>("api/documents", ct) ?? [];

    // Null means only one thing: server returned 404 for this document id.
    // Other failures still throw.
    public async Task<DocumentDto?> TryGetDocumentAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/documents/{id}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Document response body was empty.");
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(int id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<MessageDto>>($"api/documents/{id}/messages", ct) ?? [];

    public async Task<DocumentDto> UploadDocumentAsync(IBrowserFile file, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var fileContent = new StreamContent(stream);
        var contentType = file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "text/plain";
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", file.Name);

        using var response = await http.PostAsync("api/documents", content, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Upload response body was empty.");
    }

    public async Task DeleteDocumentAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/documents/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<QueryResponse> QueryDocumentAsync(int id, string question, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/documents/{id}/query",
            new QueryRequest { Question = question },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 502)
            {
                throw new ApiException(
                    "The server could not authenticate with Claude API. Check the configured API key.",
                    (int)response.StatusCode);
            }

            if ((int)response.StatusCode == 503)
            {
                throw new ApiException(
                    "The server could not reach Claude API. Please try again shortly.",
                    (int)response.StatusCode);
            }

            throw new ApiException(
                $"Query request failed with status {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<QueryResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Query response body was empty.");
    }
}
