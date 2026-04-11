using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp;
using CorruptionTracker.Crawler.Models;
using CorruptionTracker.Crawler.Repositories;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CorruptionTracker.Crawler.Services;

public class Crawler : BackgroundService
{
    private readonly ILogger<Crawler> _logger;
    private readonly IDocumentoRepository _repository;

    private readonly string[] Seeds = new[]
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
        "https://pt.wikipedia.org/wiki/Luiz_In%C3%A1cio_Lula_da_Silva",
        "https://pt.wikipedia.org/wiki/Corrup%C3%A7%C3%A3o_no_Brasil",
        "https://www.gazetadopovo.com.br/republica/suspeita-de-crimes-envolvendo-moraes-e-o-banco-master-impulsiona-pedidos-de-impeachment/",
        "https://www.bbc.com/portuguese/articles/cvg555lkw9po",
        "https://x.com/TI_InterBr/status/2034936047381983616",
        "https://www1.folha.uol.com.br/folha-topicos/corrupcao/",
        "https://www.infomoney.com.br/colunistas/economia-e-politica-direto-ao-ponto/especial-resumao-completo-sobre-a-operacao-lava-jato-e-o-petrolao/#:~:text=%E2%80%9CPetrol%C3%A3o%E2%80%9D%20%C3%A9%20um%20esquema%20bilion%C3%A1rio%20de%20corrup%C3%A7%C3%A3o%20na,cofres%20de%20partidos%2C%20funcion%C3%A1rios%20da%20estatal%20e%20pol%C3%ADticos.",
        "https://pt.wikipedia.org/wiki/Esc%C3%A2ndalo_do_mensal%C3%A3o",
        "https://g1.globo.com/politica/noticia/2025/05/02/o-que-a-pf-descobriu-na-investigacao-das-fraudes-no-inss-que-derrubou-lupi-do-governo.ghtml",
        "https://pt.wikipedia.org/wiki/Esc%C3%A2ndalo_do_Banco_Master"
    };

    private readonly Dictionary<string, int> PalavrasChave = new()
    {
        // Categoria: Alta Gravidade (Indicam o ato de corrupção diretamente)
        ["corrupção"] = 5,
        ["propina"] = 5,
        ["peculato"] = 5,
        ["suborno"] = 5,
        ["lavagem de dinheiro"] = 5,
        ["prevaricação"] = 4,
        ["concussão"] = 4,
        ["crime contra a administração"] = 4,
        ["fraudes"] = 4,
        ["rombo"] = 4,

        // Categoria: Processos e Investigação (Onde a corrupção é reportada)
        ["improbidade administrativa"] = 4,
        ["fraude em licitação"] = 4,
        ["superfaturamento"] = 4,
        ["esquema de desvio"] = 4,
        ["investigação policial"] = 3,
        ["operação deflagrada"] = 3,
        ["denúncia do ministério público"] = 3,
        ["processo administrativo disciplinar"] = 3,

        // Categoria: Termos de Alerta e Transparência
        ["indiciado"] = 2,
        ["réu"] = 2,
        ["condenação"] = 2,
        ["ilegalidade"] = 2,
        ["irregularidade"] = 2,
        ["bloqueio de bens"] = 2,
        ["favorecimento"] = 2
    };

    public Crawler(ILogger<Crawler> logger, IDocumentoRepository repository)
    {
        _logger = logger;
        _repository = repository;
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
                MaxPagesToCrawl = 20000,
                MaxCrawlDepth = 5,
                IsRespectRobotsDotTextEnabled = true,
                MinCrawlDelayPerDomainMilliSeconds = 1500,
                MaxConcurrentThreads = 10,
                UserAgentString = "CorruptionRI-Bot/1.0 (Academic)",
            };

            var crawler = new PoliteWebCrawler(config);
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
                        _logger.LogDebug("Processada (nova ou > 24h): {Url}", url[..Math.Min(80, url.Length)]);
                    }
                    else
                    {
                        _logger.LogDebug("Ignorada (< 24h ou sem relevância): {Url}", url[..Math.Min(80, url.Length)]);
                    }
                }
            };

            await crawler.CrawlAsync(new Uri(seed));
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
            var pontuacao = CalcularPontuacao(url, titulo, texto);

            // Sempre atualizar documento (mesmo que com score 0)
            var documento = new DocumentoCrawlado
            {
                HashUrl = hashUrl,
                Url = url,
                Titulo = pontuacao >= 2 ? titulo : "[Visitada - Sem Relevância]",
                Texto = pontuacao >= 2 ? texto : string.Empty,
                PontuacaoRelevancia = pontuacao,
                ColetadoEm = DateTime.UtcNow
            };

            await _repository.SalvarAsync(documento, ct);

            if (pontuacao >= 2)
            {
                _logger.LogInformation("-> [{Score}] {Title}", pontuacao, titulo[..Math.Min(60, titulo.Length)]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar página");
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
        int score = 0;

        foreach (var kv in PalavrasChave)
        {
            // Conta quantas vezes a chave aparece
            var matches = Regex.Matches(conteudo, Regex.Escape(kv.Key));
            score += matches.Count * kv.Value;
        }

        return score;
    }

    private static string GerarHashUrl(string url)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
        return Convert.ToBase64String(hash);
    }
}
