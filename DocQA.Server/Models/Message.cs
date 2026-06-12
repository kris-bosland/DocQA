namespace DocQA.Server.Models;

public class Message
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public required string Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }

    public Document Document { get; set; } = null!;
}
