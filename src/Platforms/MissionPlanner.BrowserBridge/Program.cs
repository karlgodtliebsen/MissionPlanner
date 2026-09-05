using MissionPlanner.BrowserBridge;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:7170");
var manifest = builder.Configuration["BrowserAssets"];
if (string.IsNullOrWhiteSpace(manifest) || !File.Exists(manifest))
    throw new InvalidOperationException("Pass --BrowserAssets with the full path to MissionPlanner.Browser.staticwebassets.runtime.json after building the browser project.");
builder.Configuration[WebHostDefaults.StaticWebAssetsKey] = Path.GetFullPath(manifest);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddSingleton(new UdpBridge(builder.Configuration.GetValue("UdpPort", 14550)));
var app = builder.Build();
app.UseWebSockets();
app.Map("/bridge/udp", async context => await context.RequestServices.GetRequiredService<UdpBridge>().HandleAsync(context));
app.UseDefaultFiles();
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".dat"] = "application/octet-stream"; // .NET globalization data
contentTypes.Mappings[".pdb"] = "application/octet-stream"; // Debug symbols
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
});
app.Run();
