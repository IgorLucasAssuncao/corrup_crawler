using Abot2.Core;
using Abot2.Poco;
using CorruptionTracker.Crawler.Services;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

public class PlaywrightAwarePageRequester : PageRequester
{
    private readonly PlaywrightBrowserService _playwright;
    private readonly PlaywrightDecisionService _decisionService;
    private readonly ILogger _logger;

    public PlaywrightAwarePageRequester(
        CrawlConfiguration config,
        IWebContentExtractor contentExtractor,
        PlaywrightBrowserService playwright,
        PlaywrightDecisionService decisionService,
        ILogger logger)
        : base(config, contentExtractor)
    {
        _playwright = playwright;
        _decisionService = decisionService;
        _logger = logger;
    }

    public override async Task<CrawledPage> MakeRequestAsync(
        Uri uri,
        Func<CrawledPage, CrawlDecision> shouldDownloadContent)
    {
        // Serviço de decisão já sabe que esse domínio precisa de Playwright
        if (_decisionService.DeveUsarPlaywright(uri))
        {
            _logger.LogDebug("Playwright (decisão prévia): {Url}", uri);
            return await MakePlaywrightRequestAsync(uri).ConfigureAwait(false);
        }

        // Tenta HTTP normal primeiro
        var crawledPage = await base.MakeRequestAsync(uri, (CrawledPage x) => new CrawlDecision
        {
            Allow = true
        }).ConfigureAwait(false);

        // HTML veio vazio ou insuficiente
        //if (!HtmlTemConteudo(crawledPage.Content?.Text))
        //{
        //    _logger.LogInformation(
        //        "HTML insuficiente em {Dominio}, marcando como SPA e usando Playwright",
        //        uri.Host);

        //    _decisionService.MarcarComoPlaywright(uri);
        //    return await MakePlaywrightRequestAsync(uri);
        //}

        //// HTML veio com conteúdo, marca domínio como estático
        //_decisionService.MarcarComoEstatico(uri);
        return crawledPage;
    }

    private async Task<CrawledPage> MakePlaywrightRequestAsync(Uri uri)
    {
        var crawledPage = new CrawledPage(uri)
        {
            RequestStarted = DateTime.Now
        };

        try
        {
            var html = await _playwright.RenderizarAsync(uri.AbsoluteUri);

            crawledPage.Content = new PageContent
            {
                Text = html,
                Bytes = Encoding.UTF8.GetBytes(html),
                Charset = "UTF-8"
            };

            crawledPage.HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            crawledPage.HttpRequestException =
                new HttpRequestException("Playwright falhou", ex);

            _logger.LogWarning("Playwright falhou para {Url}: {Erro}",
                uri.AbsoluteUri, ex.Message);
        }
        finally
        {
            crawledPage.RequestCompleted = DateTime.Now;
        }

        return crawledPage;
    }

    private static bool HtmlTemConteudo(string? html)
    {
        if (string.IsNullOrWhiteSpace(html) || html.Length < 500)
            return false;

        // Indicadores de SPA sem conteúdo renderizado
        var indicadoresSpa = new[]
        {
        "<div id=\"root\"></div>",
        "<div id=\"app\"></div>",
        "<div id=\"__next\"></div>",
        "ng-version=",
        "data-reactroot"
    };

        if (indicadoresSpa.Any(html.Contains))
            return false;

        // Pelo menos 2 parágrafos com conteúdo real
        return Regex.Matches(html, @"<p[^>]*>.{80,}</p>").Count >= 2;
    }
}