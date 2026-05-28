using CorruptionTracker.Api.Models;
using CorruptionTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CorruptionTracker.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(SearchService searchService, ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Busca documentos por termo usando o índice TF-IDF.
    /// </summary>
    /// <param name="q">Termo de busca</param>
    /// <param name="pagina">Página (começa em 1)</param>
    /// <param name="tamanhoPagina">Itens por página (máximo 50)</param>
    [HttpGet]
    public async Task<ActionResult<SearchResponse>> Get(
        [FromQuery] string q,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { erro = "O parâmetro 'q' é obrigatório." });

        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, 50);
        pagina = Math.Max(1, pagina);

        _logger.LogInformation("Busca: '{Termo}' | página {Pagina}", q, pagina);

        var resultado = await _searchService.BuscarAsync(q, pagina, tamanhoPagina, ct);
        return Ok(resultado);
    }
}
