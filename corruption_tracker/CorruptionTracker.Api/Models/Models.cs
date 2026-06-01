using MongoDB.Bson.Serialization.Attributes;

namespace CorruptionTracker.Api.Models;

// ─── MongoDB models (mirrors Crawler collections) ──────────────────────────

public class CrawledDocument
{
    [BsonId]
    public string UrlHash { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public DateTime CollectedAt { get; set; }
    public DateTime? IndexedAt { get; set; }
}

public class PostingEntry
{
    [BsonId]
    public string Id { get; set; } = default!;
    public string Term { get; set; } = default!;
    public string DocumentHash { get; set; } = default!;
    public int Tf { get; set; }
    public double TfIdf { get; set; }
}

// ─── Search parameters ──────────────────────────────────────────────────────

public enum SortOrder
{
    Relevance,
    Latest
}

public record SearchFilters(
    DateTime? StartDate,
    DateTime? EndDate,
    SortOrder SortBy
);

// ─── Search API response ────────────────────────────────────────────────────

public class SearchResult
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Domain { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Preview { get; set; } = default!;
    public double Score { get; set; }
    public DateTime CollectedAt { get; set; }
    public List<string> MatchedTerms { get; set; } = [];
}

public class SearchResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public List<SearchResult> Results { get; set; } = [];
}

// ─── Recent document (response for GET /documents/recent) ──────────────────

public class RecentDocument
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Domain { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Preview { get; set; } = default!;
    public DateTime CollectedAt { get; set; }
}