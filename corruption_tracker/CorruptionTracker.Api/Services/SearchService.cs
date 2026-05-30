using CorruptionTracker.Api.Models;
using Lucene.Net.Analysis.Br;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using MongoDB.Driver;

namespace CorruptionTracker.Api.Services;

public class SearchService
{
    private readonly IMongoDatabase _db;
    private readonly BrazilianAnalyzer _analyzer;

    public SearchService(IMongoDatabase db)
    {
        _db = db;
        _analyzer = new BrazilianAnalyzer(LuceneVersion.LUCENE_48);
    }

    public async Task<SearchResponse> SearchAsync(
        string query, int page, int pageSize, SearchFilters filters, CancellationToken ct)
    {
        // 1. Tokenize query using the same pipeline as the indexer
        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return new SearchResponse { Total = 0, Page = page, Results = [] };

        var postingsCollection = _db.GetCollection<PostingEntry>("postings");
        var documentsCollection = _db.GetCollection<CrawledDocument>("documentos");

        // 2. If date filters are set, pre-fetch allowed document hashes
        HashSet<string>? allowedHashes = null;
        if (filters.StartDate.HasValue || filters.EndDate.HasValue)
        {
            var dateFilter = Builders<CrawledDocument>.Filter.Empty;
            if (filters.StartDate.HasValue)
                dateFilter &= Builders<CrawledDocument>.Filter.Gte(d => d.CollectedAt, filters.StartDate.Value);
            if (filters.EndDate.HasValue)
                dateFilter &= Builders<CrawledDocument>.Filter.Lte(d => d.CollectedAt, filters.EndDate.Value.AddDays(1));

            var hashesInRange = await documentsCollection
                .Find(dateFilter)
                .Project(d => d.UrlHash)
                .ToListAsync(ct);

            allowedHashes = [.. hashesInRange];

            if (allowedHashes.Count == 0)
                return new SearchResponse { Total = 0, Page = page, Results = [] };
        }

        // 3. Accumulate TF-IDF scores per document for each query token
        var scoresByDocument = new Dictionary<string, double>();
        var matchedTermsByDocument = new Dictionary<string, HashSet<string>>();

        foreach (var token in tokens.Distinct())
        {
            var postingFilter = Builders<PostingEntry>.Filter.Eq(p => p.Term, token);
            if (allowedHashes is not null)
                postingFilter &= Builders<PostingEntry>.Filter.In(p => p.DocumentHash, allowedHashes);

            var sort = Builders<PostingEntry>.Sort.Descending(p => p.TfIdf);

            var postings = await postingsCollection
                .Find(postingFilter)
                .Sort(sort)
                .Limit(1000)
                .ToListAsync(ct);

            foreach (var posting in postings)
            {
                scoresByDocument.TryAdd(posting.DocumentHash, 0);
                scoresByDocument[posting.DocumentHash] += posting.TfIdf;

                if (!matchedTermsByDocument.ContainsKey(posting.DocumentHash))
                    matchedTermsByDocument[posting.DocumentHash] = [];
                matchedTermsByDocument[posting.DocumentHash].Add(token);
            }
        }

        if (scoresByDocument.Count == 0)
            return new SearchResponse { Total = 0, Page = page, Results = [] };

        // 4. Sort and paginate
        int totalResults = scoresByDocument.Count;
        List<string> pageHashes;

        if (filters.SortBy == SortOrder.Relevance)
        {
            pageHashes = scoresByDocument
                .OrderByDescending(kv => kv.Value)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(kv => kv.Key)
                .ToList();
        }
        else
        {
            // Sort by date: fetch all candidate hashes then let MongoDB sort by CollectedAt
            var allCandidates = scoresByDocument.Keys.ToList();
            pageHashes = await documentsCollection
                .Find(Builders<CrawledDocument>.Filter.In(d => d.UrlHash, allCandidates))
                .SortByDescending(d => d.CollectedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .Project(d => d.UrlHash)
                .ToListAsync(ct);
        }

        // 5. Hydrate current page documents
        var documents = await documentsCollection
            .Find(Builders<CrawledDocument>.Filter.In(d => d.UrlHash, pageHashes))
            .ToListAsync(ct);
        var documentsByHash = documents.ToDictionary(d => d.UrlHash);

        // 6. Build results preserving sort order
        var results = pageHashes
            .Where(hash => documentsByHash.ContainsKey(hash))
            .Select(hash =>
            {
                var doc = documentsByHash[hash];
                var preview = doc.Content.Length > 400
                    ? doc.Content[..400] + "..."
                    : doc.Content;

                return new SearchResult
                {
                    Id = hash,
                    Url = doc.Url,
                    Domain = ExtractDomain(doc.Url),
                    Title = doc.Title,
                    Preview = preview,
                    Score = scoresByDocument[hash],
                    CollectedAt = doc.CollectedAt,
                    MatchedTerms = matchedTermsByDocument.TryGetValue(hash, out var terms)
                        ? [.. terms]
                        : []
                };
            })
            .ToList();

        return new SearchResponse
        {
            Total = totalResults,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)totalResults / pageSize),
            Results = results
        };
    }

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

    private List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        using var reader = new StringReader(text);
        using var tokenStream = _analyzer.GetTokenStream("content", reader);
        var termAttribute = tokenStream.GetAttribute<ICharTermAttribute>();
        tokenStream.Reset();
        while (tokenStream.IncrementToken())
        {
            var token = termAttribute.ToString();
            if (token.Length >= 3 && token.Length <= 40 && !token.All(char.IsDigit))
                tokens.Add(token);
        }
        tokenStream.End();
        return tokens;
    }
}