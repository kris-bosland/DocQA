using Microsoft.EntityFrameworkCore;
using DocQA.Server.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "DataSource=docqa.db"));

var app = builder.Build();

app.UseHttpsRedirection();

app.Run();

public partial class Program { }
