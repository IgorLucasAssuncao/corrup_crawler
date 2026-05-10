using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp;
using CorruptionTracker.Crawler.Models;
using CorruptionTracker.Crawler.Repositories;
using CorruptionTracker.Crawler.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CorruptionTracker.Crawler.BackGroundServices;

public class Crawler : BackgroundService
{
    private readonly ILogger<Crawler> _logger;
    private readonly IDocumentoRepository _repository;
    private readonly RssHyperLinkParser _rssHyperLinkParser;
    private readonly PlaywrightBrowserService _playwrightBrowserService;
    private readonly PlaywrightDecisionService _playwrightDecisionService;

    private readonly string[] Seeds = new[]
    {
    "https://pt.wikipedia.org/wiki/Corrup%C3%A7%C3%A3o_no_Brasil",
    "https://pt.wikipedia.org/wiki/Esc%C3%A2ndalo_do_mensal%C3%A3o",
    "https://pt.wikipedia.org/wiki/Esc%C3%A2ndalo_do_Banco_Master",
    "https://pt.wikipedia.org/wiki/Opera%C3%A7%C3%A3o_Lava_Jato",
    "https://pt.wikipedia.org/wiki/Petr%C3%B3leo_Brasileiro_S.A.",
    "https://pt.wikipedia.org/wiki/Lista_de_senadores_do_Brasil",
    "https://pt.wikipedia.org/wiki/Lista_de_presidentes_do_Brasil",
    "https://www.mpf.mp.br/",
    "https://www.cgu.gov.br/noticias",
    "https://portal.tcu.gov.br/imprensa/noticias/",
    "https://theintercept.com/brasil/",
    "https://theintercept.com/feed/?rss",
    "https://piaui.folha.uol.com.br/",
    "https://g1.globo.com/politica/", 
    "https://www.estadao.com.br/politica/",
    "https://www.gazetadopovo.com.br/republica/",
    "https://g1.globo.com/politica/",
    //"https://feeds.folha.uol.com.br/poder/rss091.xml",
    "https://transparenciainternacional.org.br/posts/",
    "https://www25.senado.leg.br/web/senadores/em-exercicio",
    "https://news.google.com/rss/search?q=when:7d+lava+jato&hl=pt-BR&gl=BR&ceid=BR:pt-419",
    "https://news.google.com/rss/search?q=when:24h+corrup%C3%A7%C3%A3o+brasil&hl=pt-BR&gl=BR&ceid=BR:pt-419",
    "https://www.bing.com/news/search?q=corrup%C3%A7%C3%A3o+brasil&mkt=pt-BR&freshness=Day&format=rss",
    "https://www.bing.com/news/search?q=lava+jato&mkt=pt-BR&freshness=Week&format=rss"
    };

    public Crawler(ILogger<Crawler> logger, IDocumentoRepository repository, PlaywrightBrowserService playwrightBrowserService, PlaywrightDecisionService playwrightDecisionService)
    {
        _logger = logger;
        _repository = repository;
        _rssHyperLinkParser = new RssHyperLinkParser(logger);
        _playwrightBrowserService = playwrightBrowserService;
        _playwrightDecisionService = playwrightDecisionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando crawler de corrupção");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var tarefas = Seeds.Select(seed => RastrearSeedAsync(seed, stoppingToken)).ToList();
                await Task.WhenAll(tarefas);

                _logger.LogInformation("Ciclo concluído");
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Crawler cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no crawler");
        }
    }

    private async Task RastrearSeedAsync(string seed, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Iniciando rastreamento de: {Seed}", seed);

            var config = new CrawlConfiguration
            {
                MaxPagesToCrawl = 10000,
                MaxCrawlDepth = 5,
                IsRespectRobotsDotTextEnabled = false,
                MinCrawlDelayPerDomainMilliSeconds = 1500,
                MaxConcurrentThreads = 3,
                UserAgentString = "*",
                DownloadableContentTypes = "text/html,application/xhtml+xml,application/xml"
            };

            var crawler = new PoliteWebCrawler(
                                            config,
                                            crawlDecisionMaker: null,
                                            threadManager: null,
                                            scheduler: null,
                                            pageRequester: new PlaywrightAwarePageRequester(config, new WebContentExtractor(), _playwrightBrowserService, _playwrightDecisionService, _logger),
                                            htmlParser: _rssHyperLinkParser,
                                            memoryManager: null,
                                            domainRateLimiter: null,
                                            robotsDotTextFinder: null
                                            );
            var paginasProcessadas = 0;

            crawler.PageCrawlCompleted += async (sender, e) =>
            {
                if (e.CrawledPage.HttpResponseMessage?.IsSuccessStatusCode == true)
                {
                    var url = e.CrawledPage.Uri.AbsoluteUri;
                    var hashUrl = GerarHashUrl(url);

                    var deveConsumir = await _repository.DeveConsumirUrl(hashUrl, ct);

                    if (deveConsumir)
                    {
                        await ProcessarPaginaAsync(e.CrawledPage, ct);
                        paginasProcessadas++;
                        _logger.LogInformation("Processada (nova ou > 24h): {Url}", url[..Math.Min(80, url.Length)]);
                    }
                    else
                    {
                        _logger.LogInformation("Ignorada (< 24h ou sem relevância): {Url}", url[..Math.Min(80, url.Length)]);
                    }
                }
            };

            var result = await crawler.CrawlAsync(new Uri(seed)).ConfigureAwait(false);
            _logger.LogInformation("Seed finalizada: {Seed} | Páginas processadas: {Count}", seed, paginasProcessadas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao rastrear seed: {Seed}", seed);
        }
    }

    private async Task ProcessarPaginaAsync(CrawledPage page, CancellationToken ct)
    {
        try
        {
            var url = page.Uri.AbsoluteUri;
            var hashUrl = GerarHashUrl(url);

            var html = page.Content.Text ?? string.Empty;
            var (titulo, texto) = await ExtrairConteudoAsync(html);

            var documento = new DocumentoCrawlado
            {
                HashUrl = hashUrl,
                Url = url,
                Titulo = titulo,
                Texto = texto,
                ColetadoEm = DateTime.UtcNow,
                IndexadoEm = null
            };

            await _repository.SalvarAsync(documento, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar página");
        }
    }

    private async Task<(string titulo, string texto)> ExtrairConteudoAsync(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var documento = await context.OpenAsync(req => req.Content(html));

        var paraRemover = documento.QuerySelectorAll(
            "nav, footer, script, style, .most-read, .related-posts, .newsletter-embed, .offcanvas-wrapper, .site-header, .site-footer, .share--article"
        );
        foreach (var item in paraRemover) item.Remove();

        var container = documento.QuerySelector(".entry-content") ?? documento.Body;

        var titulo = documento.QuerySelector("h1")?.TextContent?.Trim() ?? "";

        var texto = string.Join(" ",
            container.QuerySelectorAll("p")
                .Select(e => e.TextContent?.Trim()));

        return (titulo, texto);
    }

    private static string GerarHashUrl(string url)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
        return Convert.ToBase64String(hash);
    }
}
