using CorruptionTracker.Crawler.Models;

namespace CorruptionTracker.Crawler.Repositories;

public interface IDocumentoRepository
{
    Task<DocumentoCrawlado?> ObterPorHashAsync(string hashUrl, CancellationToken ct = default);
    Task<bool> DeveConsumirUrl(string hashUrl, CancellationToken ct = default);
    Task SalvarAsync(DocumentoCrawlado documento, CancellationToken ct = default);
}
