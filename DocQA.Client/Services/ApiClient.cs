using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocQA.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace DocQA.Client.Services;

public class ApiClient(HttpClient http)
{
    public Task<IEnumerable<DocumentDto>?> GetDocumentsAsync()
        => http.GetFromJsonAsync<IEnumerable<DocumentDto>>("api/documents");

    public Task<DocumentDto?> GetDocumentAsync(int id)
        => http.GetFromJsonAsync<DocumentDto>($"api/documents/{id}");

    public async Task<DocumentDto?> UploadDocumentAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);

        var response = await http.PostAsync("api/documents", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentDto>();
    }

    public Task<HttpResponseMessage> DeleteDocumentAsync(int id)
        => http.DeleteAsync($"api/documents/{id}");

    public async Task<QueryResponse?> QueryDocumentAsync(int id, string question)
    {
        var response = await http.PostAsJsonAsync(
            $"api/documents/{id}/query",
            new QueryRequest { Question = question });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueryResponse>();
    }

    public Task<IEnumerable<MessageDto>?> GetMessagesAsync(int id)
        => http.GetFromJsonAsync<IEnumerable<MessageDto>>($"api/documents/{id}/messages");
}
