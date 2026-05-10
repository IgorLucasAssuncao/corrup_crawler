using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CorruptionTracker.Crawler.Models;

/// <summary>
/// Documento crawleado e armazenado no MongoDB.
/// Collection: "documentos"
/// </summary>
public class DocumentoCrawlado
{
    [BsonId]
    public string HashUrl { get; set; } = default!;

    public string Url { get; set; } = default!;

    public string Titulo { get; set; } = default!;

    public string Texto { get; set; } = default!;

    public DateTime ColetadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// null = pendente de indexação.
    /// Preenchido pelo Indexador após processar.
    /// Resetado para null quando o Crawler detecta texto atualizado.
    /// </summary>
    public DateTime? IndexadoEm { get; set; } = null;
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
    public string Termo { get; set; } = default!;

    /// <summary>
    /// Hash do documento (referência ao DocumentoCrawlado._id).
    /// </summary>
    public string DocHash { get; set; } = default!;

    /// <summary>
    /// Term Frequency: quantas vezes o termo aparece neste documento.
    /// </summary>
    public int Tf { get; set; }

    /// <summary>
    /// Posições do termo no texto tokenizado (para busca por frase).
    /// Ex: [0, 45, 89] = aparece na posição 0, 45 e 89.
    /// </summary>
    public List<int> Posicoes { get; set; } = [];

    /// <summary>
    /// TF-IDF score: Tf × Idf.
    /// Usado para ranking dos resultados de busca.
    /// </summary>
    public double TfIdf { get; set; }
}

/// <summary>
/// Resultado de uma busca no índice.
/// Retornado pela camada de busca para o consumidor.
/// </summary>
public class SearchResult
{
    public string DocumentoHash { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Titulo { get; set; } = default!;
    public string Preview { get; set; } = default!;
    public double Score { get; set; }
    public DateTime ColetadoEm { get; set; }
    public List<string> TermosEncontrados { get; set; } = [];
}