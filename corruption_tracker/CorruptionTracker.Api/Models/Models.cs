using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CorruptionTracker.Api.Models;

// ─── MongoDB models (mirrors Crawler collections) ──────────────────────────

/// <summary>
/// Documento crawleado e armazenado no MongoDB.
/// Collection: "documentos"
/// Mantém assinatura do Crawler com mapeamento para propriedades em inglês na API.
/// </summary>
public class CrawledDocument
{
    [BsonId]
    [BsonElement("HashUrl")]
    public string UrlHash { get; set; } = default!;

    public string Url { get; set; } = default!;

    [BsonElement("Titulo")]
    public string Title { get; set; } = default!;

    [BsonElement("Texto")]
    public string Content { get; set; } = default!;

    [BsonElement("ColetadoEm")]
    public DateTime CollectedAt { get; set; }

    [BsonElement("IndexadoEm")]
    public DateTime? IndexedAt { get; set; }
}

/// <summary>
/// Termo único do vocabulário com estatísticas globais.
/// Collection: "vocabulario"
/// 
/// Exemplo no MongoDB:
/// { _id: "corrupt", df: 1250, cf: 15000, idf: 2.08 }
/// </summary>
public class Vocabulario
{
    /// <summary>
    /// Termo stemado (ex: "corrupt", "investig", "petrobra").
    /// </summary>
    [BsonId]
    public string Termo { get; set; } = default!;

    /// <summary>
    /// Document Frequency: quantos documentos contêm este termo.
    /// </summary>
    public int Df { get; set; }

    /// <summary>
    /// Collection Frequency: total de ocorrências do termo em toda a coleção.
    /// </summary>
    public int Cf { get; set; }

    /// <summary>
    /// Inverse Document Frequency: log(totalDocs / Df).
    /// Termos raros têm IDF alto → mais relevantes.
    /// </summary>
    public double Idf { get; set; }
}

/// <summary>
/// Entrada no índice invertido: par (termo × documento).
/// Collection: "postings"
/// 
/// Exemplo no MongoDB:
/// { termo: "corrupt", docHash: "abc123", tf: 3, posicoes: [0,45,89], tfIdf: 6.24 }
/// </summary>
public class PostingEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;

    /// <summary>
    /// Termo stemado (referência ao Vocabulario._id).
    /// </summary>
    [BsonElement("Termo")]
    public string Term { get; set; } = default!;

    /// <summary>
    /// Hash do documento (referência ao DocumentoCrawlado._id).
    /// </summary>
    [BsonElement("DocHash")]
    public string DocumentHash { get; set; } = default!;

    /// <summary>
    /// Term Frequency: quantas vezes o termo aparece neste documento.
    /// </summary>
    public int Tf { get; set; }

    /// <summary>
    /// Posições do termo no texto tokenizado (para busca por frase).
    /// Ex: [0, 45, 89] = aparece na posição 0, 45 e 89.
    /// </summary>
    [BsonElement("Posicoes")]
    public List<int> Positions { get; set; } = [];

    /// <summary>
    /// TF-IDF score: Tf × Idf.
    /// Usado para ranking dos resultados de busca.
    /// </summary>
    [BsonElement("TfIdf")]
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