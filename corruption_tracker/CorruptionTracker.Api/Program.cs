using CorruptionTracker.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMongoDBClient("mongodb");

// 🚀 Register re-ranking services (ContextReRanker must be Singleton — stems pre-calculated once)
builder.Services.AddSingleton<ContextReRanker>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<RecentNewsService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
