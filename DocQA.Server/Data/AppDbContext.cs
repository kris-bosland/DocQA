using Microsoft.EntityFrameworkCore;
using DocQA.Server.Models;

namespace DocQA.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Document)
            .WithMany(d => d.Messages)
            .HasForeignKey(m => m.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
