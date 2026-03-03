using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SSRS_Subscription.Models;
using SSRS_Subscription.Services;
using SSRS_Subscription.Utils;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Settings
builder.Services.Configure<SsrsSettings>(builder.Configuration.GetSection("SsrsSettings"));

// 2. Configure the HttpClient
builder.Services.AddHttpClient<ISsrsService, SsrsService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<SsrsSettings>>().Value;
    var baseUrl = settings.BaseUrl.EndsWith("/") ? settings.BaseUrl : settings.BaseUrl + "/";
    client.BaseAddress = new Uri(baseUrl);
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<SsrsSettings>>().Value;
    return AuthHelper.GetNtlmHandler(settings);
});

// 3. Add API Controllers
builder.Services.AddControllers();

var app = builder.Build();


app.MapGet("/ping", () => "The .NET router is alive and well!");

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();