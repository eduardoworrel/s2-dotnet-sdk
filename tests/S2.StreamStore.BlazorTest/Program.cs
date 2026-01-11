using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using S2.StreamStore;
using S2.StreamStore.BlazorTest;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient for general use
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// S2 HttpClient (needs different base address)
builder.Services.AddScoped<S2Client>(sp =>
{
    var httpClient = new HttpClient();
    return new S2Client(new S2Options
    {
        AccessToken = "test-token", // Will be replaced with real token for testing
        HttpClient = httpClient
    });
});

await builder.Build().RunAsync();
