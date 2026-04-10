using CloudNativeImageProcessing.Infrastructure;
using CloudNativeImageProcessing.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkerInfrastructure(builder.Configuration);
builder.Services.AddScoped<ImageProcessingEventHandler>();
builder.Services.AddHostedService<ImageProcessingWorkerHostedService>();

var host = builder.Build();
await host.RunAsync();
