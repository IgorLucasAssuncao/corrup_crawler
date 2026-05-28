using MongoDB.Bson.Serialization.Attributes;

namespace CorruptionTracker.Api.Models;

// ─── Modelos MongoDB (mesmas collections do Crawler) ───────────────────────

public class DocumentoCrawlado
{
    [BsonId]
    public string HashUrl { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Titulo { get; set; } = default!;
    public string Texto { get; set; } = default!;
    public DateTime ColetadoEm { get; set; }
    public DateTime? IndexadoEm { get; set; }
}

public class PostingEntry
{
    [BsonId]
    public string Id { get; set; } = default!;
    public string Termo { get; set; } = default!;
    public string DocHash { get; set; } = default!;
    public int Tf { get; set; }
    public double TfIdf { get; set; }
}

// ─── Resposta da API de busca ───────────────────────────────────────────────

public class SearchResult
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Titulo { get; set; } = default!;
    public string Preview { get; set; } = default!;
    public double Score { get; set; }
    public DateTime ColetadoEm { get; set; }
    public List<string> TermosEncontrados { get; set; } = [];
}

public class SearchResponse
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TotalPaginas { get; set; }
    public List<SearchResult> Resultados { get; set; } = [];
}
