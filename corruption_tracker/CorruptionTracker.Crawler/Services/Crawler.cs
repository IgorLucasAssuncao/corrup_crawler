using System.Security.Cryptography;
using System.Text;
using AngleSharp;
using Abot2.Crawler;
using Abot2.Poco;
using MongoDB.Driver;
using CorruptionTracker.Crawler.Models;

namespace CorruptionTracker.Crawler.Services;

public class Crawler : BackgroundService
{
    private readonly ILogger<Crawler> _logger;
    private readonly IMongoDatabase _mongoClient;

    private static readonly string[] Seeds = new[]
    {
        "https://g1.globo.com/",
        "https://www.folha.uol.com.br/",
        "https://www.estadao.com.br/",
        "https://apublica.org",
        "https://theintercept.com/",
        "https://www.portaldatransparencia.gov.br",
        "https://www.cgu.gov.br/",
        "https://www.mpf.mp.br/",
        "https://www.jusbrasil.com.br/",
        "https://transparenciainternacional.org.br",
    };

    private static readonly Dictionary<string, int> PalavrasChave = new()
    {
        ["corrupção"] = 3,
        ["propina"] = 3,
        ["peculato"] = 3,
        ["lavagem de dinheiro"] = 3,
        ["improbidade"] = 2,
        ["superfaturamento"] = 2,
        ["fraude"] = 1,
        ["suborno"] = 1,
        ["investigação"] = 1,
        ["indiciado"] = 1,
    };

    public Crawler(ILogger<Crawler> logger, IMongoDatabase mongoClient)
    {
        _logger = logger;
        _mongoClient = mongoClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Iniciando crawler de corrupção");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var colecao = _mongoClient.GetCollection<DocumentoCrawlado>("documentos");
                var saved = 0;

                var tarefas = Seeds.Select(seed => RastrearSeedAsync(seed, colecao, stoppingToken)).ToList();
                await Task.WhenAll(tarefas);

                _logger.LogInformation("✅ Ciclo concluído | Documentos salvos: {Count}", saved);
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🛑 Crawler cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro no crawler");
        }
    }

    private async Task RastrearSeedAsync(string seed, IMongoCollection<DocumentoCrawlado> colecao, CancellationToken ct)
    {
        try
        {
            var config = new CrawlConfiguration
            {
                MaxPagesToCrawl = 100_000,
                MaxCrawlDepth = 4,
                IsRespectRobotsDotTextEnabled = true,
                MinCrawlDelayPerDomainMilliSeconds = 1500,
                MaxConcurrentThreads = 10,
                UserAgentString = "CorruptionRI-Bot/1.0 (Academic)",
            };

            var crawler = new PoliteWebCrawler(config);

            crawler.PageCrawlCompleted += async (sender, e) =>
            {
                if (e.CrawledPage.HttpResponseMessage?.IsSuccessStatusCode == true)
                {
                    await ProcessarPaginaAsync(e.CrawledPage, colecao, ct);
                }
            };

            await crawler.CrawlAsync(new Uri(seed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao rastrear seed: {Seed}", seed);
        }
    }

    private async Task ProcessarPaginaAsync(CrawledPage page, IMongoCollection<DocumentoCrawlado> colecao, CancellationToken ct)
    {
        try
        {
            var html = page.Content.Text ?? string.Empty;
            var (titulo, texto) = await ExtrairConteudoAsync(html);
            var pontuacao = CalcularPontuacao(page.Uri.AbsoluteUri, titulo, texto);

            if (pontuacao >= 2)
            {
                var documento = new DocumentoCrawlado
                {
                    HashUrl = GerarHashUrl(page.Uri.AbsoluteUri),
                    Url = page.Uri.AbsoluteUri,
                    Titulo = titulo,
                    Texto = texto,
                    PontuacaoRelevancia = pontuacao,
                    ColetadoEm = DateTime.UtcNow
                };

                var filtro = Builders<DocumentoCrawlado>.Filter.Eq(d => d.HashUrl, documento.HashUrl);
                var opcoes = new ReplaceOptions { IsUpsert = true };
                await colecao.ReplaceOneAsync(filtro, documento, opcoes, ct);

                _logger.LogInformation("✅ [{Score}] {Title}", pontuacao, titulo[..Math.Min(60, titulo.Length)]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar página");
        }
    }

    private async Task<(string titulo, string texto)> ExtrairConteudoAsync(string html)
    {
        try
        {
            var context = BrowsingContext.New(Configuration.Default);
            var documento = await context.OpenAsync(req => req.Content(html));

            var titulo = documento.QuerySelector("h1")?.TextContent?.Trim()
                         ?? documento.QuerySelector("title")?.TextContent?.Trim()
                         ?? documento.Title?.Trim()
                         ?? string.Empty;

            var texto = string.Join(" ",
                documento.QuerySelectorAll("p, article, main, section")
                    .Select(e => e.TextContent?.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 30)
                    .ToList());

            return (titulo, texto);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private int CalcularPontuacao(string url, string titulo, string texto)
    {
        var conteudo = $"{url} {titulo} {texto}".ToLowerInvariant();
        return PalavrasChave
            .Where(kv => conteudo.Contains(kv.Key))
            .Sum(kv => kv.Value);
    }

    private static string GerarHashUrl(string url)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
        return Convert.ToBase64String(hash);
    }
}
