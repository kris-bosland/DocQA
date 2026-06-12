namespace DocQA.Server.Models;

public class Document
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string Content { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
