using System.Reflection;
using CloudNativeImageProcessing.AiGenerationWorker;
using CloudNativeImageProcessing.Infrastructure;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}

builder.Services.AddAiGenerationWorkerInfrastructure(builder.Configuration);
builder.Services.AddScoped<AiGenerationEventHandler>();
builder.Services.AddHostedService<AiGenerationWorkerHostedService>();

var host = builder.Build();
await host.RunAsync();
