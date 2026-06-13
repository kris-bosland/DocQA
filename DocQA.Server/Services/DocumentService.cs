using System.Text;
using DocQA.Server.Data;
using DocQA.Server.Models;
using DocQA.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DocQA.Server.Services;

public class DocumentService(AppDbContext db, ILogger<DocumentService> logger) : IDocumentService
{
    public async Task<DocumentDto> CreateAsync(IFormFile file, CancellationToken ct = default)
    {
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var content = await reader.ReadToEndAsync(ct);

        var doc = new Document
        {
            FileName = Path.GetFileName(file.FileName),
            Content = content,
            FileSizeBytes = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created document {Id} ({FileName})", doc.Id, doc.FileName);
        return ToDto(doc, includeContent: true);
    }

    /// <summary>
    /// Retrieves all documents without their content, ordered by upload date (newest first).
    /// </summary>
    /// <remarks>
    /// Content is excluded for performance; use <see cref="GetByIdAsync"/> for full details.
    /// </remarks>
    public async Task<IEnumerable<DocumentDto>> GetAllAsync(CancellationToken ct = default) =>
        await db.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                FileSizeBytes = d.FileSizeBytes,
                UploadedAt = d.UploadedAt,
                Content = null
            })
            .ToListAsync(ct);

    public async Task<DocumentDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var doc = await db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        return doc is null ? null : ToDto(doc, includeContent: true);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var rows = await db.Documents
            .Where(d => d.Id == id)
            .ExecuteDeleteAsync(ct);

        if (rows > 0)
            logger.LogInformation("Deleted document {Id}", id);

        return rows > 0;
    }

    private static DocumentDto ToDto(Document doc, bool includeContent) => new()
    {
        Id = doc.Id,
        FileName = doc.FileName,
        FileSizeBytes = doc.FileSizeBytes,
        UploadedAt = doc.UploadedAt,
        Content = includeContent ? doc.Content : null
    };
}
