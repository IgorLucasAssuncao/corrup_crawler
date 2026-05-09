using Abot2.Core;
using Abot2.Poco;
using AngleSharp.Html.Dom;
using System.Xml.Linq;

public class RssHyperLinkParser : AngleSharpHyperlinkParser
{
    private readonly ILogger _logger;

    public RssHyperLinkParser(ILogger logger)
    {
        _logger = logger;
    }

    protected override IEnumerable<HyperLink> GetRawHyperLinks(
        CrawledPage crawledPage)
    {
        var contentType = crawledPage.HttpResponseMessage?
            .Content.Headers.ContentType?.MediaType ?? "";

        // Se for XML (sitemap), usa o parser de sitemap
        if (contentType.Contains("xml") ||
            crawledPage.Uri.AbsoluteUri.Contains("sitemap"))
        {
            _logger.LogInformation("Processando sitemap: {Url}",
                crawledPage.Uri.AbsoluteUri);
            return ParsearRss(crawledPage);
        }

        // Se for HTML normal, usa o comportamento padrão do Abot2
        return base.GetRawHyperLinks(crawledPage);
    }

    private IEnumerable<HyperLink> ParsearRss(CrawledPage crawledPage)
    {
        var links = new List<HyperLink>();

        try
        {
            var xml = XDocument.Parse(crawledPage.Content.Text);

            var itens = xml.Descendants()
                .Where(e => e.Name.LocalName == "item");

            foreach (var item in itens)
            {
                var url = item.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "link")?.Value?.Trim();

                if (!string.IsNullOrWhiteSpace(url) &&
                    Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    links.Add(new HyperLink { HrefValue = uri, RawHrefText = url, RawHrefValue = url});
            }

            _logger.LogInformation("RSS {Url}: {Count} links encontrados",
                crawledPage.Uri.AbsoluteUri, links.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Erro ao parsear RSS {Url}: {Erro}",
                crawledPage.Uri.AbsoluteUri, ex.Message);
        }

        return links;
    }
}