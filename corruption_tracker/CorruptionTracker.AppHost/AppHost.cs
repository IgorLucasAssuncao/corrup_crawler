var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
                    .WithMongoExpress()
                   .WithLifetime(ContainerLifetime.Persistent);

var mongodb = mongo.AddDatabase("mongodb");

builder.AddProject<Projects.CorruptionTracker_Crawler>("crawler")
    .WithReference(mongodb)
    .WaitFor(mongodb);

builder.Build().Run();
