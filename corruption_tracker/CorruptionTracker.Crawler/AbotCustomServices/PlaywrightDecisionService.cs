using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Abot2.Core;
using Abot2.Poco;

namespace CorruptionTracker.Crawler.Services;

// ═══════════════════════════════════════════════════════
// SERVIÇO DE DECISÃO
// Responsável por decidir se uma URL deve usar Playwright
// ═══════════════════════════════════════════════════════
public class PlaywrightDecisionService
{
    private readonly ILogger<PlaywrightDecisionService> _logger;

    private readonly ConcurrentDictionary<string, bool> _dominioCache = new();

    // Domínios que sabemos de antemão que são SPA
    private static readonly HashSet<string> _dominiosSpaConhecidos = new()
    {
         "news.google.com"
    };

    public PlaywrightDecisionService(ILogger<PlaywrightDecisionService> logger)
    {
        _logger = logger;
    }

    public bool DeveUsarPlaywright(Uri uri)
    {
        var dominio = uri.Host;

        if (_dominiosSpaConhecidos.Contains(dominio))
            return true;

        return _dominioCache.GetValueOrDefault(dominio, false);
    }

    //public void MarcarComoPlaywright(Uri uri)
    //{
    //    var dominio = uri.Host;
    //    _dominioCache[dominio] = true;
    //    _logger.LogInformation("Domínio {Dominio} marcado como SPA", dominio);
    //}

    //public void MarcarComoEstatico(Uri uri)
    //{
    //    _dominioCache.TryAdd(uri.Host, false);
    //}

    //public IReadOnlyDictionary<string, bool> ObterDicionario()
    //    => _dominioCache;
}