using System.Reflection;
using CloudNativeImageProcessing.AiGenerationWorker;
using CloudNativeImageProcessing.Infrastructure;
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

builder.Services.AddAiGenerationWorkerInfrastructure(builder.Configuration);
builder.Services.AddScoped<AiGenerationEventHandler>();
builder.Services.AddHostedService<AiGenerationWorkerHostedService>();

var host = builder.Build();
await host.RunAsync();
