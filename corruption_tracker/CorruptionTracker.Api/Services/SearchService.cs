using CorruptionTracker.Api.Models;
using Lucene.Net.Analysis.Br;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using MongoDB.Driver;

namespace CorruptionTracker.Api.Services;

/// <summary>
/// Serviço de busca usando o índice invertido TF-IDF construído pelo Indexer.
/// Pipeline:
///   1. Tokeniza + stemma o termo de busca (mesmo analisador do Indexer)
///   2. Para cada token, busca os postings ordenados por TF-IDF
///   3. Soma os scores por documento (BM25-like)
///   4. Hidrata com os dados do DocumentoCrawlado
///   5. Retorna paginado
/// </summary>
public class SearchService
{
    private readonly IMongoDatabase _db;
    private readonly BrazilianAnalyzer _analyzer;

    public SearchService(IMongoDatabase db)
    {
        _db = db;
        _analyzer = new BrazilianAnalyzer(LuceneVersion.LUCENE_48);
    }

    public async Task<SearchResponse> BuscarAsync(
        string query, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        // 1. Tokenizar a query (mesmo pipeline do indexer)
        var tokens = Tokenizar(query);

        if (tokens.Count == 0)
            return new SearchResponse { Total = 0, Pagina = pagina, Resultados = [] };

        // 2. Para cada token, buscar postings no MongoDB
        var colecaoPostings = _db.GetCollection<PostingEntry>("postings");
        var colecaoDocs = _db.GetCollection<DocumentoCrawlado>("documentos");

        var scoresPorDoc = new Dictionary<string, double>();
        var termosPorDoc = new Dictionary<string, HashSet<string>>();

        foreach (var token in tokens.Distinct())
        {
            var filtro = Builders<PostingEntry>.Filter.Eq(p => p.Termo, token);
            var sort = Builders<PostingEntry>.Sort.Descending(p => p.TfIdf);

            // Limita a 1000 postings por token para não explodir a memória
            var postings = await colecaoPostings
                .Find(filtro)
                .Sort(sort)
                .Limit(1000)
                .ToListAsync(ct);

            foreach (var posting in postings)
            {
                scoresPorDoc.TryAdd(posting.DocHash, 0);
                scoresPorDoc[posting.DocHash] += posting.TfIdf;

                if (!termosPorDoc.ContainsKey(posting.DocHash))
                    termosPorDoc[posting.DocHash] = [];
                termosPorDoc[posting.DocHash].Add(token);
            }
        }

        if (scoresPorDoc.Count == 0)
            return new SearchResponse { Total = 0, Pagina = pagina, Resultados = [] };

        // 3. Ordenar por score e paginar
        var totalResultados = scoresPorDoc.Count;
        var hashesOrdenados = scoresPorDoc
            .OrderByDescending(kv => kv.Value)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(kv => kv.Key)
            .ToList();

        // 4. Hidratar com os dados do documento
        var filtroDocs = Builders<DocumentoCrawlado>.Filter.In(d => d.HashUrl, hashesOrdenados);
        var documentos = await colecaoDocs.Find(filtroDocs).ToListAsync(ct);
        var docsPorHash = documentos.ToDictionary(d => d.HashUrl);

        // 5. Montar resultados (mantendo a ordem por score)
        var resultados = hashesOrdenados
            .Where(hash => docsPorHash.ContainsKey(hash))
            .Select(hash =>
            {
                var doc = docsPorHash[hash];
                var preview = doc.Texto.Length > 300
                    ? doc.Texto[..300] + "..."
                    : doc.Texto;

                return new SearchResult
                {
                    Id = hash,
                    Url = doc.Url,
                    Titulo = doc.Titulo,
                    Preview = preview,
                    Score = scoresPorDoc[hash],
                    ColetadoEm = doc.ColetadoEm,
                    TermosEncontrados = termosPorDoc.TryGetValue(hash, out var t)
                        ? [.. t]
                        : []
                };
            })
            .ToList();

        return new SearchResponse
        {
            Total = totalResultados,
            Pagina = pagina,
            TotalPaginas = (int)Math.Ceiling((double)totalResultados / tamanhoPagina),
            Resultados = resultados
        };
    }

    private List<string> Tokenizar(string texto)
    {
        var tokens = new List<string>();
        using var reader = new StringReader(texto);
        using var tokenStream = _analyzer.GetTokenStream("content", reader);
        var termAttr = tokenStream.GetAttribute<ICharTermAttribute>();
        tokenStream.Reset();
        while (tokenStream.IncrementToken())
        {
            var token = termAttr.ToString();
            if (token.Length >= 3 && token.Length <= 40 && !token.All(char.IsDigit))
                tokens.Add(token);
        }
        tokenStream.End();
        return tokens;
    }
}
