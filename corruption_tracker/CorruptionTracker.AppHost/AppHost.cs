var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo", port: 27017)
                    .WithMongoExpress()
                    .WithLifetime(ContainerLifetime.Persistent)
                    .WithDataVolume("MongoData");

var mongodb = mongo.AddDatabase("mongodb");

builder.AddProject<Projects.CorruptionTracker_Crawler>("crawler")
    .WithReference(mongodb)
    .WaitFor(mongodb);

builder.Build().Run();
