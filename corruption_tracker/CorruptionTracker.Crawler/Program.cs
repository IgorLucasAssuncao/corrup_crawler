using Microsoft.Extensions.Hosting;
using CorruptionTracker.Crawler.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddMongoDBClient("mongodb");

builder.Services.AddHttpClient();
builder.Services.AddHostedService<Crawler>();

builder.Build().Run();
