using DocQA.Shared;
using Microsoft.AspNetCore.Http;

namespace DocQA.Server.Services;

public interface IDocumentService
{
    Task<DocumentDto> CreateAsync(IFormFile file, CancellationToken ct = default);
    Task<IEnumerable<DocumentDto>> GetAllAsync(CancellationToken ct = default);
    Task<DocumentDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
