using CorruptionTracker.Crawler.Models;
using MongoDB.Driver;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CorruptionTracker.Crawler.Repositories;

// Classe auxiliar para estruturar os dados no Cache
public class CacheDocumentoInfo
{
    public DateTime ColetadoEm { get; set; }
    public int Pontuacao { get; set; }
}

public class DocumentoRepository : IDocumentoRepository
{
    private readonly IMongoDatabase _mongoDb;
    private readonly ILogger<DocumentoRepository> _logger;
    private readonly IDistributedCache _cache;
    private const string CacheKeyPrefix = "url:";
    private const int CacheTtlHours = 24;

    public DocumentoRepository(IMongoDatabase mongoDb, ILogger<DocumentoRepository> logger, IDistributedCache cache)
    {
        _mongoDb = mongoDb;
        _logger = logger;
        _cache = cache;
    }

    public async Task<DocumentoCrawlado?> ObterPorHashAsync(string hashUrl, CancellationToken ct = default)
    {
        try
        {
            var colecao = _mongoDb.GetCollection<DocumentoCrawlado>("documentos");
            var filtro = Builders<DocumentoCrawlado>.Filter.Eq(d => d.HashUrl, hashUrl);
            return await colecao.Find(filtro).FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter documento por hash: {Hash}", hashUrl);
            return null;
        }
    }

    public async Task<bool> DeveConsumirUrl(string hashUrl, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = CacheKeyPrefix + hashUrl;
            var jsonCache = await _cache.GetStringAsync(cacheKey, ct);

            if (!string.IsNullOrEmpty(jsonCache))
            {
                var info = JsonSerializer.Deserialize<CacheDocumentoInfo>(jsonCache);

                if (info!.Pontuacao == 0)
                    return false;

                if ((DateTime.UtcNow - info.ColetadoEm).TotalHours < 24)
                    return false;
            }

            var documento = await ObterPorHashAsync(hashUrl, ct);
            if (documento == null) return true;

            // Atualiza o cache com os dados atuais do banco
            await SalvarNoCacheAsync(hashUrl, documento.ColetadoEm, documento.PontuacaoRelevancia, ct);

            return (DateTime.UtcNow - documento.ColetadoEm).TotalHours < 24;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status da URL: {Hash}", hashUrl);
            return false;
        }
    }

    public async Task SalvarAsync(DocumentoCrawlado documento, CancellationToken ct = default)
    {
        try
        {
            var colecao = _mongoDb.GetCollection<DocumentoCrawlado>("documentos");
            var filtro = Builders<DocumentoCrawlado>.Filter.Eq(d => d.HashUrl, documento.HashUrl);
            var opcoes = new ReplaceOptions { IsUpsert = true };

            await colecao.ReplaceOneAsync(filtro, documento, opcoes, ct);

            // Salva no cache a data e a pontuação
            await SalvarNoCacheAsync(documento.HashUrl, documento.ColetadoEm, documento.PontuacaoRelevancia, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar documento no repositório");
        }
    }

    private async Task SalvarNoCacheAsync(string hashUrl, DateTime data, int pontuacao, CancellationToken ct)
    {
        var info = new CacheDocumentoInfo { ColetadoEm = data, Pontuacao = pontuacao };
        var json = JsonSerializer.Serialize(info);

        await _cache.SetStringAsync(
            CacheKeyPrefix + hashUrl,
            json,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheTtlHours)
            },
            ct);
    }
}