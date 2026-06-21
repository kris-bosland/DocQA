using Anthropic.SDK;
using DocQA.Server.Data;
using DocQA.Server.Endpoints;
using DocQA.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var clientOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ??
    [
        "https://witty-ground-04822531e.7.azurestaticapps.net",
        "https://localhost:7149",
        "http://localhost:5149"
    ];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
            clientOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) ||
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (
                uri.Host.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            ))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseCors();

app.MapDocumentEndpoints();
app.MapQueryEndpoints();
app.MapSystemEndpoints();

app.Run();

public partial class Program { }
