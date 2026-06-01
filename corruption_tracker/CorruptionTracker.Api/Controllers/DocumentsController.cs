using CorruptionTracker.Api.Models;
using CorruptionTracker.Crawler.Models;
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

        var collection = _db.GetCollection<DocumentoCrawlado>("documentos");

        var documents = await collection
            .Find(FilterDefinition<DocumentoCrawlado>.Empty)
            .SortByDescending(d => d.ColetadoEm)
            .Limit(count)
            .ToListAsync(ct);

        var result = documents.Select(d => new RecentDocument
        {
            Id = d.HashUrl,
            Url = d.Url,
            Domain = ExtractDomain(d.Url),
            Title = d.Titulo,
            Preview = d.Texto.Length > 300 ? d.Texto[..300] + "..." : d.Texto,
            CollectedAt = d.ColetadoEm,
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