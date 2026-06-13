using DocQA.Server.Data;
using DocQA.Server.Endpoints;
using DocQA.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "DataSource=docqa.db"));

builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapDocumentEndpoints();

app.Run();

public partial class Program { }
