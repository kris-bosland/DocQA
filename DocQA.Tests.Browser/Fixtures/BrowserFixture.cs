using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DocQA.Server.Data;
using DocQA.Server.Endpoints;
using DocQA.Server.Models;
using DocQA.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Playwright;

namespace DocQA.Tests.Browser.Fixtures;

public sealed class BrowserFixture : IAsyncLifetime
{
    private const int MaxStartAttempts = 3;

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private string? _baseUrl;
    private string? _publishRoot;
    private string? _clientWebRoot;
    private WebApplication? _app;

    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string AppBaseUrl => _baseUrl ?? throw new InvalidOperationException("Browser fixture is not initialized.");

    public async Task InitializeAsync()
    {
        _connection.Open();

        for (var attempt = 1; attempt <= MaxStartAttempts; attempt++)
        {
            var baseUrl = $"http://127.0.0.1:{GetAvailablePort()}";
            var publishRoot = Path.Combine(Path.GetTempPath(), $"docqa-client-publish-{Guid.NewGuid():N}");
            var clientWebRoot = CreateClientWebRoot(baseUrl, publishRoot);

            try
            {
                var app = BuildHost(baseUrl, clientWebRoot);
                await app.StartAsync();

                _baseUrl = baseUrl;
                _publishRoot = publishRoot;
                _clientWebRoot = clientWebRoot;
                _app = app;

                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.EnsureCreatedAsync();
                }

                Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

                return;
            }
            catch (Exception ex) when (IsAddressInUse(ex) && attempt < MaxStartAttempts)
            {
                SafeDeleteDirectory(clientWebRoot);
                SafeDeleteDirectory(publishRoot);
            }
            catch
            {
                SafeDeleteDirectory(clientWebRoot);
                SafeDeleteDirectory(publishRoot);
                throw;
            }
        }

        throw new InvalidOperationException("Could not start browser fixture host after multiple attempts.");
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();

        Playwright?.Dispose();

        if (_app is not null)
            await _app.StopAsync();

        await _connection.DisposeAsync();

        SafeDeleteDirectory(_clientWebRoot);
        SafeDeleteDirectory(_publishRoot);
    }

    private WebApplication BuildHost(string baseUrl, string clientWebRoot)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            WebRootPath = clientWebRoot
        });

        var webRootFileProvider = new PhysicalFileProvider(clientWebRoot);
        builder.Environment.WebRootPath = clientWebRoot;
        builder.Environment.WebRootFileProvider = webRootFileProvider;
        builder.WebHost.UseKestrel().UseUrls(baseUrl);
        builder.Services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_connection));
        builder.Services.AddScoped<IDocumentService, DocumentService>();
        builder.Services.AddSingleton<IClaudeService, StubClaudeService>();

        var app = builder.Build();
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = webRootFileProvider,
            ContentTypeProvider = contentTypeProvider
        });
        app.MapDocumentEndpoints();
        app.MapQueryEndpoints();

        var indexFile = Path.Combine(clientWebRoot, "index.html");
        app.MapGet("/", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(await File.ReadAllTextAsync(indexFile));
        });

        app.MapFallback(async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(await File.ReadAllTextAsync(indexFile));
        });

        return app;
    }

    private static string CreateClientWebRoot(string baseUrl, string publishRoot)
    {
        var solutionRoot = FindSolutionRoot();
        PublishClient(solutionRoot, publishRoot);

        var publishedWebRoot = Path.Combine(publishRoot, "wwwroot");
        if (!Directory.Exists(publishedWebRoot))
            throw new DirectoryNotFoundException($"Could not find published client web root under '{publishedWebRoot}'.");

        var tempRoot = Path.Combine(Path.GetTempPath(), $"docqa-browser-{Guid.NewGuid():N}");
        CopyDirectory(publishedWebRoot, tempRoot);

        File.WriteAllText(
            Path.Combine(tempRoot, "appsettings.json"),
            $"{{\n  \"ApiBaseUrl\": \"{baseUrl}\"\n}}\n");

        return tempRoot;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsAddressInUse(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketException
                && socketException.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return true;
            }

            if (current.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void SafeDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        Directory.Delete(path, recursive: true);
    }

    private static void PublishClient(string solutionRoot, string outputDirectory)
    {
        var clientProject = Path.Combine(solutionRoot, "DocQA.Client", "DocQA.Client.csproj");
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{clientProject}\" -c Release -o \"{outputDirectory}\"",
            WorkingDirectory = solutionRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet publish for DocQA.Client.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish DocQA.Client failed with exit code {process.ExitCode}.\n{output}\n{error}");
        }
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 4 && directory is not null; i++)
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate solution root.");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}