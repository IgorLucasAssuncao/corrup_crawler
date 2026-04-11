using MongoDB.Bson.Serialization.Attributes;

namespace CorruptionTracker.Crawler.Models;

public class DocumentoCrawlado
{
    [BsonId]
    public string HashUrl { get; set; } = default!;

    public string Url { get; set; } = default!;

    public string Titulo { get; set; } = default!;

    public string Texto { get; set; } = default!;

    public int PontuacaoRelevancia { get; set; }

    public DateTime ColetadoEm { get; set; } = DateTime.UtcNow;
}
