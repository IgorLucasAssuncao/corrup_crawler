using CorruptionTracker.Api.Models;
using CorruptionTracker.Crawler.Models;
using Lucene.Net.Analysis.Br;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace CorruptionTracker.Api.Services;

public class SearchService
{
    private readonly IMongoDatabase _db;
    private readonly ContextReRanker _reranker;
    private readonly BrazilianAnalyzer _analyzer;
    private readonly ILogger<SearchService> _logger;

    private const int MongoInThreshold = 50_000;
    private const int CandidatePoolNoFilter = 500;
    private const int CandidatePoolWithFilter = 5_000;
    private const int PostingsLimitNoFilter = 5_000;

    private const double CosineWeight = 0.75;
    private const double ContextWeight = 0.25;

    // Thresholds bem permissivos — fallback resolve o resto
    private const double MinScoreWithFilter = 0.01;
    private const double MinScoreWithoutFilter = 0.02;

    private const double FullMatchBoost = 1.4;
    private const int FallbackTextSearchLimit = 500;

    private readonly Dictionary<string, double> _idfCache = new();
    private long _cachedTotalDocs = -1;
    private DateTime _idfCacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan IdfCacheTtl = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _idfLock = new(1, 1);

    public SearchService(IMongoDatabase db, ContextReRanker reranker, ILogger<SearchService> logger)
    {
        _db = db;
        _reranker = reranker;
        _logger = logger;
        _analyzer = new BrazilianAnalyzer(LuceneVersion.LUCENE_48);
    }

    public async Task<SearchResponse> SearchAsync(
        string query, int page, int pageSize, SearchFilters filters, CancellationToken ct)
    {
        _logger.LogInformation("═══════════════════════════════════════════");
        _logger.LogInformation("🔎 BUSCA: '{Query}' | page={Page} size={Size}", query, page, pageSize);

        var (stemmedTokens, rawTokens) = TokenizeBoth(query);

        if (stemmedTokens.Count == 0 && rawTokens.Count == 0)
        {
            _logger.LogWarning("❌ Tokenização não gerou termos válidos");
            return Empty(page);
        }

        var searchTokens = stemmedTokens.Union(rawTokens).Distinct().ToList();
        var queryTermGroups = BuildTermGroups(stemmedTokens, rawTokens);
        int totalQueryGroups = queryTermGroups.Count;

        _logger.LogInformation("📝 Tokens stemmed: [{S}]", string.Join(",", stemmedTokens));
        _logger.LogInformation("📝 Tokens raw:     [{R}]", string.Join(",", rawTokens));
        _logger.LogInformation("📝 Search tokens:  [{T}] ({Count} únicos)", string.Join(",", searchTokens), searchTokens.Count);
        _logger.LogInformation("📝 Grupos:         {Groups} grupos lógicos", totalQueryGroups);

        var postingsCollection = _db.GetCollection<Crawler.Models.PostingEntry>("postings");
        var documentsCollection = _db.GetCollection<DocumentoCrawlado>("documentos");

        bool hasFilters = filters.StartDate.HasValue || filters.EndDate.HasValue;

        // ───── ETAPA 1: FILTRO ─────
        HashSet<string>? allowedHashes = null;
        if (hasFilters)
        {
            allowedHashes = await ResolveAllowedHashesAsync(documentsCollection, filters, ct);
            _logger.LogInformation("🎯 Filtro: {Count} docs no universo", allowedHashes.Count);
            if (allowedHashes.Count == 0) return Empty(page);
        }

        // ───── ETAPA 2: IDF + POSTINGS (paralelo) ─────
        var idfTask = ComputeIdfAsync(postingsCollection, documentsCollection, searchTokens, ct);
        var postingsTask = CollectPostingsAsync(postingsCollection, searchTokens, allowedHashes, hasFilters, ct);

        await Task.WhenAll(idfTask, postingsTask);

        var idfByTerm = await idfTask;
        var (postingsByDoc, matchedTokensByDocument) = await postingsTask;

        _logger.LogInformation("📊 IDFs: [{Idfs}]",
            string.Join(", ", idfByTerm.Select(kv => $"{kv.Key}={kv.Value:F2}")));
        _logger.LogInformation("📚 {Count} docs casaram com pelo menos 1 token", postingsByDoc.Count);

        // ───── FALLBACK 1: índice invertido vazio ─────
        if (postingsByDoc.Count == 0)
        {
            _logger.LogWarning("⚠️ FALLBACK 1: nenhum token achou postings — usando regex");
            return await DoFallbackRegexAsync(documentsCollection, rawTokens, allowedHashes, filters, page, pageSize, ct);
        }

        // ───── ETAPA 3: VETOR DA QUERY ─────
        var queryVector = new Dictionary<string, double>();
        foreach (var token in searchTokens)
        {
            if (idfByTerm.TryGetValue(token, out var idf) && idf > 0)
                queryVector[token] = idf;
        }

        // Defesa: se IDF zerou tudo, força peso 1.0 para cada token
        if (queryVector.Count == 0)
        {
            _logger.LogWarning("⚠️ IDF zerou todos os tokens — usando peso uniforme 1.0");
            foreach (var token in searchTokens)
                queryVector[token] = 1.0;
        }

        double queryNorm = Math.Sqrt(queryVector.Values.Sum(v => v * v));
        _logger.LogInformation("📐 Vetor query: {Dims} dims | ||q||={Norm:F2}", queryVector.Count, queryNorm);

        // ───── ETAPA 4: SCORE PRINCIPAL ─────
        // Cosseno simplificado: dotProduct / (||q|| × √(numTermosDoc))
        // Usa quantidade de postings do doc como proxy barato de ||d||
        // (não precisa fetch extra — funciona bem para ranking)
        var scores = new Dictionary<string, double>(postingsByDoc.Count);
        var coverageByDoc = new Dictionary<string, int>(postingsByDoc.Count);
        var coverageHistogram = new int[totalQueryGroups + 1];

        foreach (var (hash, termsTfIdf) in postingsByDoc)
        {
            // Produto interno q·d (sobre termos da query)
            double dotProduct = 0;
            foreach (var (term, tfidf) in termsTfIdf)
            {
                if (queryVector.TryGetValue(term, out var qWeight))
                    dotProduct += qWeight * tfidf;
            }

            if (dotProduct <= 0) continue;

            // Cobertura por GRUPO (não conta variantes stemmed/raw como termos diferentes)
            int matchedGroups = queryTermGroups.Count(g => g.Any(t => termsTfIdf.ContainsKey(t)));
            coverageByDoc[hash] = matchedGroups;
            coverageHistogram[matchedGroups]++;

            double coverage = (double)matchedGroups / totalQueryGroups;

            // Normalização leve por número de termos casados (proxy de ||d||)
            // Quanto mais termos da query o doc tem, melhor
            double pseudoNorm = Math.Sqrt(termsTfIdf.Count);
            double cosineProxy = dotProduct / (queryNorm * pseudoNorm);

            // Boost de cobertura (multiplicativo, ao quadrado para amplificar)
            double score = cosineProxy * (0.3 + 0.7 * coverage * coverage);

            // Boost full-match
            if (matchedGroups == totalQueryGroups && totalQueryGroups > 1)
                score *= FullMatchBoost;

            scores[hash] = score;
        }

        // Histograma de cobertura — diagnóstico crítico
        for (int i = totalQueryGroups; i >= 1; i--)
        {
            if (coverageHistogram[i] > 0)
                _logger.LogInformation("📊 {Count} docs com cobertura {I}/{Total}",
                    coverageHistogram[i], i, totalQueryGroups);
        }

        if (scores.Count == 0)
        {
            _logger.LogWarning("⚠️ FALLBACK 2: scores zerados — usando regex");
            return await DoFallbackRegexAsync(documentsCollection, rawTokens, allowedHashes, filters, page, pageSize, ct);
        }

        _logger.LogInformation("✅ {Count} docs com score > 0 | Top: {Max:F3} | Médio: {Avg:F3}",
            scores.Count, scores.Values.Max(), scores.Values.Average());

        // ───── ETAPA 5: SELEÇÃO DE CANDIDATOS ─────
        int poolSize = hasFilters
            ? Math.Min(CandidatePoolWithFilter, scores.Count)
            : Math.Min(CandidatePoolNoFilter, scores.Count);

        var topCandidates = SelectByCoverageTier(scores, coverageByDoc, poolSize);
        _logger.LogInformation("🎯 {Count} candidatos selecionados", topCandidates.Count);

        // ───── ETAPA 6: HIDRATAÇÃO + RE-RANK ─────
        var candidateHashes = topCandidates.Keys.ToList();
        var candidateDocs = await HydrateAsync(documentsCollection, candidateHashes, ct);

        Dictionary<string, ContextAnalysis> contexts;
        try
        {
            contexts = await _reranker.AnalyzeBatchAsync(candidateDocs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Re-ranker falhou — usando só score base");
            contexts = new Dictionary<string, ContextAnalysis>();
        }

        var finalScores = new Dictionary<string, double>(candidateDocs.Count);
        foreach (var doc in candidateDocs)
        {
            if (!topCandidates.TryGetValue(doc.HashUrl, out var baseScore)) continue;

            double contextScore = contexts.TryGetValue(doc.HashUrl, out var ctx) ? ctx.Score : 0.0;
            double finalScore = baseScore * CosineWeight + contextScore * ContextWeight;
            finalScores[doc.HashUrl] = finalScore;
        }

        _logger.LogInformation("📊 Re-rank: {Count} docs | Top: {Max:F3} | Médio: {Avg:F3}",
            finalScores.Count,
            finalScores.Values.DefaultIfEmpty(0).Max(),
            finalScores.Values.DefaultIfEmpty(0).Average());

        // ───── ETAPA 7: THRESHOLD COM CASCADE ─────
        double minScore = hasFilters ? MinScoreWithFilter : MinScoreWithoutFilter;
        var filtered = finalScores
            .Where(kv => kv.Value >= minScore)
            .OrderByDescending(kv => kv.Value)
            .ToList();

        _logger.LogInformation("✂️ Threshold {T}: {Pass}/{Total} passaram",
            minScore, filtered.Count, finalScores.Count);

        // Cascata de fallbacks
        if (filtered.Count == 0)
        {
            _logger.LogWarning("🔄 Threshold zerou — usando top 100 sem corte");
            filtered = finalScores.OrderByDescending(kv => kv.Value).Take(100).ToList();
        }

        if (filtered.Count == 0)
        {
            _logger.LogWarning("🔄 Sem nada em finalScores — usando scores base");
            filtered = topCandidates.OrderByDescending(kv => kv.Value).Take(100).ToList();
        }

        if (filtered.Count == 0)
        {
            _logger.LogError("❌ Tudo zerou — retornando vazio");
            return Empty(page);
        }

        int totalResults = filtered.Count;

        // ───── ETAPA 8: PAGINAÇÃO ─────
        var pageHashes = await PaginateAsync(documentsCollection, filtered, filters.SortBy, page, pageSize, ct);
        var documents = await HydrateAsync(documentsCollection, pageHashes, ct);
        var documentsByHash = documents.ToDictionary(d => d.HashUrl);

        var results = pageHashes
            .Where(hash => documentsByHash.ContainsKey(hash))
            .Select(hash => BuildResult(
                documentsByHash[hash],
                finalScores.TryGetValue(hash, out var fs) ? fs : (filtered.FirstOrDefault(kv => kv.Key == hash).Value),
                matchedTokensByDocument))
            .ToList();

        _logger.LogInformation("✅ Retornando {Count} resultados (de {Total} totais)", results.Count, totalResults);
        _logger.LogInformation("═══════════════════════════════════════════");

        return new SearchResponse
        {
            Total = totalResults,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)totalResults / pageSize),
            Results = results
        };
    }

    // ═══════════════════════════════════════════════════════════
    // FALLBACK REGEX como caminho completo
    // ═══════════════════════════════════════════════════════════
    private async Task<SearchResponse> DoFallbackRegexAsync(
        IMongoCollection<DocumentoCrawlado> documentsCollection,
        List<string> rawTokens,
        HashSet<string>? allowedHashes,
        SearchFilters filters,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (rawTokens.Count == 0)
        {
            _logger.LogWarning("❌ Fallback regex sem tokens raw");
            return Empty(page);
        }

        var orFilters = new List<FilterDefinition<DocumentoCrawlado>>();
        foreach (var token in rawTokens)
        {
            var pattern = new BsonRegularExpression($"\\b{Regex.Escape(token)}\\b", "i");
            orFilters.Add(Builders<DocumentoCrawlado>.Filter.Or(
                Builders<DocumentoCrawlado>.Filter.Regex(d => d.Texto, pattern),
                Builders<DocumentoCrawlado>.Filter.Regex(d => d.Titulo, pattern)
            ));
        }

        var combined = Builders<DocumentoCrawlado>.Filter.Or(orFilters);
        if (allowedHashes is not null)
            combined &= Builders<DocumentoCrawlado>.Filter.In(d => d.HashUrl, allowedHashes);

        var docs = await documentsCollection.Find(combined).Limit(FallbackTextSearchLimit).ToListAsync(ct);
        _logger.LogInformation("🔄 Fallback regex: {Count} docs encontrados", docs.Count);

        if (docs.Count == 0) return Empty(page);

        var scores = new Dictionary<string, double>();
        var matched = new Dictionary<string, HashSet<string>>();

        foreach (var doc in docs)
        {
            var textLower = (doc.Texto + " " + doc.Titulo).ToLowerInvariant();
            double score = 0;
            int hits = 0;
            var hitTokens = new HashSet<string>();

            foreach (var token in rawTokens)
            {
                var pattern = $"\\b{Regex.Escape(token.ToLowerInvariant())}\\b";
                int count = Regex.Matches(textLower, pattern).Count;
                if (count > 0)
                {
                    double tf = Math.Log(count + 1);
                    if (doc.Titulo?.ToLowerInvariant().Contains(token.ToLowerInvariant()) == true) tf *= 2.0;
                    score += tf;
                    hits++;
                    hitTokens.Add(token);
                }
            }

            if (rawTokens.Count > 1)
                score *= (double)hits / rawTokens.Count;

            if (score > 0)
            {
                scores[doc.HashUrl] = score;
                matched[doc.HashUrl] = hitTokens;
            }
        }

        if (scores.Count == 0) return Empty(page);

        var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
        int total = sorted.Count;

        var pageHashes = await PaginateAsync(documentsCollection, sorted, filters.SortBy, page, pageSize, ct);
        var pageDocs = await HydrateAsync(documentsCollection, pageHashes, ct);
        var byHash = pageDocs.ToDictionary(d => d.HashUrl);

        var results = pageHashes
            .Where(h => byHash.ContainsKey(h))
            .Select(h => BuildResult(byHash[h], scores[h], matched))
            .ToList();

        return new SearchResponse
        {
            Total = total,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            Results = results
        };
    }

    // ═══════════════════════════════════════════════════════════
    // SELEÇÃO POR TIER DE COBERTURA
    // ═══════════════════════════════════════════════════════════
    private static Dictionary<string, double> SelectByCoverageTier(
        Dictionary<string, double> scores,
        Dictionary<string, int> coverage,
        int poolSize)
    {
        var result = new Dictionary<string, double>(poolSize);
        var byTier = scores
            .GroupBy(kv => coverage.TryGetValue(kv.Key, out var c) ? c : 0)
            .OrderByDescending(g => g.Key);

        foreach (var tier in byTier)
        {
            foreach (var kv in tier.OrderByDescending(x => x.Value))
            {
                if (result.Count >= poolSize) return result;
                result[kv.Key] = kv.Value;
            }
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // IDF
    // ═══════════════════════════════════════════════════════════
    private async Task<Dictionary<string, double>> ComputeIdfAsync(
        IMongoCollection<Crawler.Models.PostingEntry> postingsCollection,
        IMongoCollection<DocumentoCrawlado> documentsCollection,
        List<string> tokens,
        CancellationToken ct)
    {
        await _idfLock.WaitAsync(ct);
        try
        {
            if (_cachedTotalDocs < 0 || DateTime.UtcNow > _idfCacheExpiry)
            {
                _cachedTotalDocs = await documentsCollection.CountDocumentsAsync(
                    FilterDefinition<DocumentoCrawlado>.Empty, cancellationToken: ct);
                _idfCacheExpiry = DateTime.UtcNow.Add(IdfCacheTtl);
                _idfCache.Clear();
                _logger.LogInformation("♻️ IDF cache renovado | N={N} docs", _cachedTotalDocs);
            }

            var result = new Dictionary<string, double>();
            var toCompute = new List<string>();

            foreach (var token in tokens)
            {
                if (_idfCache.TryGetValue(token, out var cached))
                    result[token] = cached;
                else
                    toCompute.Add(token);
            }

            if (toCompute.Count > 0)
            {
                var tasks = toCompute.Select(async t =>
                {
                    var df = await postingsCollection.CountDocumentsAsync(
                        Builders<Crawler.Models.PostingEntry>.Filter.Eq(p => p.Termo, t),
                        cancellationToken: ct);
                    return (t, df);
                });
                var results = await Task.WhenAll(tasks);

                foreach (var (token, df) in results)
                {
                    double idf = df > 0
                        ? Math.Log((1.0 + _cachedTotalDocs) / (1.0 + df)) + 1.0
                        : 0;
                    _idfCache[token] = idf;
                    result[token] = idf;
                }
            }

            return result;
        }
        finally
        {
            _idfLock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // POSTINGS
    // ═══════════════════════════════════════════════════════════
    private async Task<(Dictionary<string, Dictionary<string, double>>, Dictionary<string, HashSet<string>>)>
        CollectPostingsAsync(
            IMongoCollection<Crawler.Models.PostingEntry> postingsCollection,
            List<string> tokens, HashSet<string>? allowedHashes, bool hasFilters, CancellationToken ct)
    {
        var byDoc = new Dictionary<string, Dictionary<string, double>>();
        var matched = new Dictionary<string, HashSet<string>>();
        bool useInMemoryFilter = allowedHashes is { Count: > MongoInThreshold };

        foreach (var token in tokens)
        {
            var f = Builders<Crawler.Models.PostingEntry>.Filter.Eq(p => p.Termo, token);
            if (allowedHashes is not null && !useInMemoryFilter)
                f &= Builders<Crawler.Models.PostingEntry>.Filter.In(p => p.DocHash, allowedHashes);

            var find = postingsCollection.Find(f)
                .Sort(Builders<Crawler.Models.PostingEntry>.Sort.Descending(p => p.TfIdf));

            var postings = hasFilters
                ? await find.ToListAsync(ct)
                : await find.Limit(PostingsLimitNoFilter).ToListAsync(ct);

            int kept = 0;
            foreach (var p in postings)
            {
                if (useInMemoryFilter && !allowedHashes!.Contains(p.DocHash)) continue;

                if (!byDoc.TryGetValue(p.DocHash, out var terms))
                {
                    terms = new Dictionary<string, double>();
                    byDoc[p.DocHash] = terms;
                }
                terms[p.Termo] = p.TfIdf;

                if (!matched.TryGetValue(p.DocHash, out var set)) { set = []; matched[p.DocHash] = set; }
                set.Add(token);
                kept++;
            }

            _logger.LogInformation("🔍 '{Token}': {Kept} postings retidos", token, kept);
        }

        return (byDoc, matched);
    }

    // ═══════════════════════════════════════════════════════════
    // TOKENIZAÇÃO DUAL
    // ═══════════════════════════════════════════════════════════
    private (List<string> stemmed, List<string> raw) TokenizeBoth(string text)
    {
        var stemmed = new List<string>();
        try
        {
            using var reader = new StringReader(text);
            using var tokenStream = _analyzer.GetTokenStream("content", reader);
            var termAttribute = tokenStream.GetAttribute<ICharTermAttribute>();
            tokenStream.Reset();
            while (tokenStream.IncrementToken())
            {
                var token = termAttribute.ToString();
                if (token.Length >= 3 && token.Length <= 40 && !token.All(char.IsDigit))
                    stemmed.Add(token);
            }
            tokenStream.End();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro no analyzer — usando só raw");
        }

        var raw = Regex.Matches(text.ToLowerInvariant(), @"[\p{L}]{3,40}")
            .Select(m => m.Value).Where(t => !t.All(char.IsDigit)).Distinct().ToList();

        return (stemmed, raw);
    }

    private static List<HashSet<string>> BuildTermGroups(List<string> stemmed, List<string> raw)
    {
        var groups = new List<HashSet<string>>();
        int max = Math.Max(stemmed.Count, raw.Count);
        for (int i = 0; i < max; i++)
        {
            var group = new HashSet<string>();
            if (i < stemmed.Count) group.Add(stemmed[i]);
            if (i < raw.Count) group.Add(raw[i]);
            if (group.Count > 0) groups.Add(group);
        }
        return groups;
    }

    private async Task<HashSet<string>> ResolveAllowedHashesAsync(
        IMongoCollection<DocumentoCrawlado> documentsCollection,
        SearchFilters filters, CancellationToken ct)
    {
        var dateFilter = Builders<DocumentoCrawlado>.Filter.Empty;
        if (filters.StartDate.HasValue)
        {
            var start = DateTime.SpecifyKind(filters.StartDate.Value.Date, DateTimeKind.Utc);
            dateFilter &= Builders<DocumentoCrawlado>.Filter.Gte(d => d.ColetadoEm, start);
        }
        if (filters.EndDate.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(filters.EndDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            dateFilter &= Builders<DocumentoCrawlado>.Filter.Lt(d => d.ColetadoEm, endExclusive);
        }
        var hashes = await documentsCollection.Find(dateFilter).Project(d => d.HashUrl).ToListAsync(ct);
        return [.. hashes];
    }

    private static async Task<List<string>> PaginateAsync(
        IMongoCollection<DocumentoCrawlado> documentsCollection,
        List<KeyValuePair<string, double>> filtered, SortOrder sortBy, int page, int pageSize, CancellationToken ct)
    {
        if (sortBy == SortOrder.Relevance)
            return filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(kv => kv.Key).ToList();

        var hashes = filtered.Select(kv => kv.Key).ToList();
        return await documentsCollection
            .Find(Builders<DocumentoCrawlado>.Filter.In(d => d.HashUrl, hashes))
            .SortByDescending(d => d.ColetadoEm).Skip((page - 1) * pageSize).Limit(pageSize)
            .Project(d => d.HashUrl).ToListAsync(ct);
    }

    private static Task<List<DocumentoCrawlado>> HydrateAsync(
        IMongoCollection<DocumentoCrawlado> documentsCollection, List<string> hashes, CancellationToken ct)
    {
        if (hashes.Count == 0) return Task.FromResult(new List<DocumentoCrawlado>());
        return documentsCollection.Find(Builders<DocumentoCrawlado>.Filter.In(d => d.HashUrl, hashes)).ToListAsync(ct);
    }

    private static Models.SearchResult BuildResult(
        DocumentoCrawlado doc, double score, Dictionary<string, HashSet<string>> matchedTerms)
    {
        var preview = doc.Texto.Length > 400 ? doc.Texto[..400] + "..." : doc.Texto;
        return new Models.SearchResult
        {
            Id = doc.HashUrl,
            Url = doc.Url,
            Domain = ExtractDomain(doc.Url),
            Title = doc.Titulo,
            Preview = preview,
            Score = score,
            CollectedAt = doc.ColetadoEm,
            MatchedTerms = matchedTerms.TryGetValue(doc.HashUrl, out var t) ? [.. t] : []
        };
    }

    private static SearchResponse Empty(int page) => new() { Total = 0, Page = page, TotalPages = 0, Results = [] };

    private static string ExtractDomain(string url)
    {
        try { var uri = new Uri(url); var h = uri.Host; return h.StartsWith("www.") ? h[4..] : h; }
        catch { return url; }
    }
}