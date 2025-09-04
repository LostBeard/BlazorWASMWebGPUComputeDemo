using BlazorWASMWebGPUComputeDemo;
using BlazorWASMWebGPUComputeDemo.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.WebWorkers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
//  Services
builder.Services.AddBlazorJSRuntime(out var JS);
builder.Services.AddWebWorkerService(o =>
{
    o.TaskPool.MaxPoolSize = o.MaxWorkerCount;
});
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<ShaderLoader>();

if (JS.IsWindow)
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}
await builder.Build().BlazorJSRunAsync();
