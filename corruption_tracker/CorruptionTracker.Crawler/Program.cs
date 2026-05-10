using Abot2.Core;
using CorruptionTracker.Crawler.BackGroundServices;
using CorruptionTracker.Crawler.Repositories;
using CorruptionTracker.Crawler.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache(connectionName: "cache");
builder.AddMongoDBClient("mongodb");

builder.Services.AddSingleton<IDocumentoRepository, DocumentoRepository>();

builder.Services.AddSingleton<PlaywrightBrowserService>();
builder.Services.AddSingleton<PlaywrightDecisionService>();
builder.Services.AddSingleton<IWebContentExtractor, WebContentExtractor>();

builder.Services.AddHostedService<Crawler>();
builder.Services.AddHostedService<IndexBackgroundService>();

builder.Build().Run();
