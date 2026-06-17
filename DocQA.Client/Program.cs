using DocQA.Client;
using DocQA.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Value in wwwroot/appsettings.json takes precedence,
// but can be overridden by environment variable or other configuration sources
// For local development, set ApiBaseUrl to "https://localhost:7221" (or the appropriate URL/port)
// For deployment, set `ApiBaseUrl` to the deployed API URL (for example, your Azure App Service URL).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<ApiClient>();

await builder.Build().RunAsync();
