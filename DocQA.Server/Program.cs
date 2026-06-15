using Anthropic.SDK;
using DocQA.Server.Data;
using DocQA.Server.Endpoints;
using DocQA.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "DataSource=docqa.db"));

builder.Services.AddScoped<IDocumentService, DocumentService>();

var apiKey = builder.Configuration["Anthropic:ApiKey"];
builder.Services.AddSingleton(_ => apiKey is { Length: > 0 }
    ? new AnthropicClient(apiKey)
    : new AnthropicClient());
builder.Services.AddSingleton<IMessageSender, AnthropicMessageSender>();
builder.Services.AddSingleton<IClaudeService, ClaudeService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapDocumentEndpoints();
app.MapQueryEndpoints();

app.Run();

public partial class Program { }
