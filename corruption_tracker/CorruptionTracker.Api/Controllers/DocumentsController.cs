using CorruptionTracker.Api.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace CorruptionTracker.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMongoDatabase _db;

    public DocumentsController(IMongoDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the most recently crawled documents.
    /// Used to populate the home page before the user performs a search.
    /// </summary>
    /// <param name="count">Number of documents to return (max 50)</param>
    [HttpGet("recent")]
    public async Task<ActionResult<List<RecentDocument>>> GetRecent(
        [FromQuery] int count = 12,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 50);

        var collection = _db.GetCollection<CrawledDocument>("documentos");

        var documents = await collection
            .Find(FilterDefinition<CrawledDocument>.Empty)
            .SortByDescending(d => d.CollectedAt)
            .Limit(count)
            .ToListAsync(ct);

        var result = documents.Select(d => new RecentDocument
        {
            Id = d.UrlHash,
            Url = d.Url,
            Domain = ExtractDomain(d.Url),
            Title = d.Title,
            Preview = d.Content.Length > 300 ? d.Content[..300] + "..." : d.Content,
            CollectedAt = d.CollectedAt,
        }).ToList();

        return Ok(result);
    }

    private static string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            return host.StartsWith("www.") ? host[4..] : host;
        }
        catch { return url; }
    }
}