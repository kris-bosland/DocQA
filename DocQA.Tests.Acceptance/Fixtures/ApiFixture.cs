using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DocQA.Server.Data;
using DocQA.Server.Services;

namespace DocQA.Tests.Acceptance.Fixtures;

public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace production DbContext with a shared :memory: SQLite connection
            var descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_connection));

            // Stub IClaudeService (safe even before Phase 4 registers it)
            var claudeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IClaudeService));
            if (claudeDescriptor is not null)
                services.Remove(claudeDescriptor);
            services.AddSingleton<IClaudeService, StubClaudeService>();
        });
    }

    public async Task InitializeAsync()
    {
        var db = Services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }
}
