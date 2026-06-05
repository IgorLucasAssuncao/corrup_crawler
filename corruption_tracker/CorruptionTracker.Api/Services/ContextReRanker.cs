using CorruptionTracker.Api.Models;
using CorruptionTracker.Crawler.Models;
using Lucene.Net.Analysis.Br;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using MongoDB.Driver;

namespace CorruptionTracker.Api.Services;

/// <summary>
/// Analysis result for a document's political/corruption context.
/// </summary>
public record ContextAnalysis
{
    public double Score { get; init; }
    public bool IsAmbiguous { get; init; }
    public int PoliticianHits { get; init; }
    public int OperationHits { get; init; }
    public int InstitutionHits { get; init; }
    public int CorruptionTfSum { get; init; }
    public double DomainTrust { get; init; }
}

/// <summary>
/// Re-ranker using inverted index (postings collection) for efficient batch analysis.
/// Pre-stems all dictionaries at startup to ensure consistency with BrazilianAnalyzer.
/// </summary>
public class ContextReRanker
{
    private readonly IMongoCollection<PostingEntry> _postings;
    private readonly BrazilianAnalyzer _analyzer;
    private readonly ILogger<ContextReRanker> _logger;

    /// <summary>
    /// Pre-computed stems for politicians (calculated at startup).
    /// </summary>
    public HashSet<string> PoliticianStems { get; }

    /// <summary>
    /// Pre-computed stems for operations.
    /// </summary>
    public HashSet<string> OperationStems { get; }

    /// <summary>
    /// Pre-computed stems for institutions.
    /// </summary>
    public HashSet<string> InstitutionStems { get; }

    /// <summary>
    /// Pre-computed stems for corruption keywords with their weights.
    /// </summary>
    public Dictionary<string, int> CorruptionStemWeights { get; }

    /// <summary>
    /// Pre-computed stems for corruption keywords.
    /// </summary>
    public HashSet<string> CorruptionStems { get; }

    /// <summary>
    /// Union of all context stems (for efficient batch query).
    /// </summary>
    public HashSet<string> AllContextStems { get; }

    public ContextReRanker(IMongoDatabase db, ILogger<ContextReRanker> logger)
    {
        _postings = db.GetCollection<PostingEntry>("postings");
        _analyzer = new BrazilianAnalyzer(LuceneVersion.LUCENE_48);
        _logger = logger;

        // Pre-stem all dictionaries at startup
        PoliticianStems = StemSet(ContextDictionary.Politicians);
        OperationStems = StemSet(ContextDictionary.Operations);
        InstitutionStems = StemSet(ContextDictionary.Institutions);

        // For corruption keywords, preserve weight per stem
        CorruptionStemWeights = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (term, weight) in ContextDictionary.CorruptionKeywords)
        {
            foreach (var stem in Tokenize(term))
            {
                // Use max weight if stem appears in multiple terms
                if (!CorruptionStemWeights.ContainsKey(stem) || CorruptionStemWeights[stem] < weight)
                    CorruptionStemWeights[stem] = weight;
            }
        }
        CorruptionStems = new HashSet<string>(CorruptionStemWeights.Keys, StringComparer.Ordinal);

        // Union for efficient batch query
        AllContextStems = new HashSet<string>(
            PoliticianStems
                .Concat(OperationStems)
                .Concat(InstitutionStems)
                .Concat(CorruptionStems),
            StringComparer.Ordinal);

        _logger.LogInformation(
            "✅ ContextReRanker initialized | Stems: Pol={P} Op={O} Inst={I} Corr={C} | Total={T}",
            PoliticianStems.Count, OperationStems.Count, InstitutionStems.Count,
            CorruptionStems.Count, AllContextStems.Count);
    }

    /// <summary>
    /// Batch analyze documents using a single MongoDB query on postings index.
    /// Much more efficient than per-document queries.
    /// </summary>
    public async Task<Dictionary<string, ContextAnalysis>> AnalyzeBatchAsync(
        List<DocumentoCrawlado> docs,
        CancellationToken ct)
    {
        if (docs.Count == 0)
            return new Dictionary<string, ContextAnalysis>();

        var docHashes = docs.Select(d => d.HashUrl).ToList();

        // 🚀 Single batch query on postings index
        var filter = Builders<PostingEntry>.Filter.And(
            Builders<PostingEntry>.Filter.In(p => p.DocHash, docHashes),
            Builders<PostingEntry>.Filter.In(p => p.Termo, AllContextStems.ToList())
        );

        var hits = await _postings
            .Find(filter)
            .ToListAsync(ct);

        // Group hits by document hash
        var hitsByDoc = hits
            .GroupBy(h => h.DocHash)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Analyze each document
        var results = new Dictionary<string, ContextAnalysis>();

        foreach (var doc in docs)
        {
            var docHits = hitsByDoc.TryGetValue(doc.HashUrl, out var hits_list) 
                ? hits_list 
                : new List<PostingEntry>();

            // Count distinct context types
            var politicianHits = docHits.Count(h => PoliticianStems.Contains(h.Termo));
            var operationHits = docHits.Count(h => OperationStems.Contains(h.Termo));
            var institutionHits = docHits.Count(h => InstitutionStems.Contains(h.Termo));

            // Sum weighted TF for corruption keywords
            var corruptionTfSum = docHits
                .Where(h => CorruptionStems.Contains(h.Termo))
                .Sum(h => h.Tf * CorruptionStemWeights[h.Termo]);

            // Detect ambiguity using title (no stemming needed, just presence check)
            var tituloLower = doc.Titulo.ToLowerInvariant();
            var hasLavaJato = tituloLower.Contains("lava jato", StringComparison.OrdinalIgnoreCase)
                           || tituloLower.Contains("lava-jato", StringComparison.OrdinalIgnoreCase)
                           || tituloLower.Contains("lava a jato", StringComparison.OrdinalIgnoreCase);

            var nonPoliticalInTitle = ContextDictionary.NonPoliticalContext
                .Count(t => tituloLower.Contains(t, StringComparison.OrdinalIgnoreCase));

            var hasPoliticalContext = politicianHits > 0 || operationHits > 1 || institutionHits > 0;
            var isAmbiguous = hasLavaJato && nonPoliticalInTitle >= 1 && !hasPoliticalContext;

            // Calculate context score
            double score;
            if (isAmbiguous)
            {
                score = 0.1;
            }
            else
            {
                // corruptionTfSum is already weighted by keyword weights
                var corruptionScore = Math.Min(corruptionTfSum * 0.05, 1.5);
                score = corruptionScore
                      + Math.Min(politicianHits * 0.4, 1.5)
                      + Math.Min(operationHits * 0.3, 1.0)
                      + Math.Min(institutionHits * 0.2, 0.8);
            }

            // Apply domain trust
            var domain = ExtractDomain(doc.Url);
            var trust = ContextDictionary.DomainTrust.TryGetValue(domain, out var t) ? t : 1.0;
            score *= trust;

            results[doc.HashUrl] = new ContextAnalysis
            {
                Score = score,
                IsAmbiguous = isAmbiguous,
                PoliticianHits = politicianHits,
                OperationHits = operationHits,
                InstitutionHits = institutionHits,
                CorruptionTfSum = corruptionTfSum,
                DomainTrust = trust
            };
        }

        return results;
    }

    /// <summary>
    /// Stem a set of natural language terms using BrazilianAnalyzer.
    /// </summary>
    private HashSet<string> StemSet(IEnumerable<string> terms)
    {
        var stems = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            foreach (var stem in Tokenize(term))
            {
                if (stem.Length >= 2)
                    stems.Add(stem);
            }
        }
        return stems;
    }

    /// <summary>
    /// Tokenize text using BrazilianAnalyzer (returns stems).
    /// </summary>
    private List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        using var reader = new StringReader(text);
        using var tokenStream = _analyzer.GetTokenStream("content", reader);
        var termAttribute = tokenStream.GetAttribute<ICharTermAttribute>();
        tokenStream.Reset();
        while (tokenStream.IncrementToken())
        {
            tokens.Add(termAttribute.ToString());
        }
        tokenStream.End();
        return tokens;
    }

    /// <summary>
    /// Extract domain from URL for trust lookup.
    /// </summary>
    private static string ExtractDomain(string url)
    {
        try
        {
            var host = new Uri(url).Host;
            return host.StartsWith("www.") ? host[4..] : host;
        }
        catch
        {
            return "";
        }
    }
}
