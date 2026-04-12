using System.Reflection;
using CloudNativeImageProcessing.Infrastructure;
using CloudNativeImageProcessing.Worker;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetryWorkerService(options =>
    {
        options.EnableAdaptiveSampling = false;
    });
}

builder.Services.AddWorkerInfrastructure(builder.Configuration);
builder.Services.AddScoped<ImageProcessingEventHandler>();
builder.Services.AddHostedService<ImageProcessingWorkerHostedService>();

var host = builder.Build();
await host.RunAsync();
