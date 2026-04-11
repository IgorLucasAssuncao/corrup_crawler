using Microsoft.Extensions.Hosting;
using CorruptionTracker.Crawler.Services;
using CorruptionTracker.Crawler.Repositories;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache(connectionName: "cache");
builder.AddMongoDBClient("mongodb");

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDocumentoRepository, DocumentoRepository>();
builder.Services.AddHostedService<Crawler>();

builder.Build().Run();
