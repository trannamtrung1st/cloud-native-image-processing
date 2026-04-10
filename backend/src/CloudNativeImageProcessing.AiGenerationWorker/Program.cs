using CloudNativeImageProcessing.AiGenerationWorker;
using CloudNativeImageProcessing.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkerInfrastructure(builder.Configuration);
builder.Services.AddScoped<AiGenerationEventHandler>();
builder.Services.AddHostedService<AiGenerationWorkerHostedService>();

var host = builder.Build();
await host.RunAsync();
