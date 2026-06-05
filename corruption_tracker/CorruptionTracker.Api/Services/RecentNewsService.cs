using CorruptionTracker.Api.Models;
using CorruptionTracker.Crawler.Models;
using MongoDB.Driver;

namespace CorruptionTracker.Api.Services;

/// <summary>
/// Provides recent news using the inverted index for efficient filtering.
/// Queries postings to find documents with corruption-related terms,
/// then re-ranks them contextually.
/// </summary>
public class RecentNewsService
{
    private readonly IMongoDatabase _db;
    private readonly ContextReRanker _reranker;
    private readonly ILogger<RecentNewsService> _logger;

    public RecentNewsService(IMongoDatabase db, ContextReRanker reranker, ILogger<RecentNewsService> logger)
    {
        _db = db;
        _reranker = reranker;
        _logger = logger;
    }

    /// <summary>
    /// Get recent documents filtered by corruption context using inverted index.
    /// </summary>
    public async Task<List<RecentDocument>> GetRecentAsync(int limit = 12, CancellationToken ct = default)
    {
        var postings = _db.GetCollection<Crawler.Models.PostingEntry>("postings");
        var documentos = _db.GetCollection<DocumentoCrawlado>("documentos");

        // 🚀 Step 1: Use inverted index to find document hashes containing corruption-related terms
        var corruptionTerms = _reranker.AllContextStems.ToList();
        
        var candidateHashes = await postings
            .Distinct<string>(
                "docHash",
                Builders<Crawler.Models.PostingEntry>.Filter.In(p => p.Termo, corruptionTerms),
                cancellationToken: ct)
            .ToListAsync(ct);

        if (candidateHashes.Count == 0)
        {
            _logger.LogDebug("No recent corruption-related documents found in postings index");
            return [];
        }

        // Step 2: Fetch the 50 most recent from this filtered subset
        const int poolSize = 50;
        var recentDocs = await documentos
            .Find(Builders<DocumentoCrawlado>.Filter.In(d => d.HashUrl, candidateHashes))
            .SortByDescending(d => d.ColetadoEm)
            .Limit(poolSize)
            .ToListAsync(ct);

        if (recentDocs.Count == 0)
            return [];

        // Step 3: 🚀 Batch re-rank using inverted index (single query on postings)
        var contexts = await _reranker.AnalyzeBatchAsync(recentDocs, ct);

        // Step 4: Filter ambiguous and low-scoring results
        const double recentMinScore = 0.2;
        var ranked = recentDocs
            .Where(d => !contexts[d.HashUrl].IsAmbiguous && contexts[d.HashUrl].Score >= recentMinScore)
            .OrderByDescending(d => d.ColetadoEm)
            .ThenByDescending(d => contexts[d.HashUrl].Score)
            .Take(limit)
            .Select(d => MapToRecent(d))
            .ToList();

        _logger.LogDebug(
            "Recent news: fetched {PoolSize} candidates, {Filtered} passed filters, returning {Result}",
            recentDocs.Count, 
            recentDocs.Count(d => !contexts[d.HashUrl].IsAmbiguous && contexts[d.HashUrl].Score >= recentMinScore),
            ranked.Count);

        return ranked;
    }

    /// <summary>
    /// Map DocumentoCrawlado to RecentDocument response model.
    /// </summary>
    private static RecentDocument MapToRecent(DocumentoCrawlado doc)
    {
        var preview = doc.Texto.Length > 300 ? doc.Texto[..300] + "..." : doc.Texto;
        return new RecentDocument
        {
            Id = doc.HashUrl,
            Url = doc.Url,
            Domain = ExtractDomain(doc.Url),
            Title = doc.Titulo,
            Preview = preview,
            CollectedAt = doc.ColetadoEm,
        };
    }

    /// <summary>
    /// Extract domain from URL.
    /// </summary>
    private static string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            return host.StartsWith("www.") ? host[4..] : host;
        }
        catch
        {
            return url;
        }
    }
}
