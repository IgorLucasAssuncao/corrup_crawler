var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo", port: 27017)
                    .WithLifetime(ContainerLifetime.Persistent)
                    .WithDataVolume("MongoData");

var mongodb = mongo.AddDatabase("mongodb");

var redis = builder.AddRedis("cache", port: 6379)
                   .WithDataVolume("RedisData")
                   .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.CorruptionTracker_Crawler>("crawler")
    .WithReference(mongodb)
    .WithReference(redis)
    .WaitFor(redis)
    .WaitFor(mongodb);

// Novo projeto de API — expõe a busca ao frontend
builder.AddProject<Projects.CorruptionTracker_Api>("api")
    .WithReference(mongodb)
    .WaitFor(mongodb)
    .WithHttpsEndpoint(port: 7000, name: "https");

builder.Build().Run();
