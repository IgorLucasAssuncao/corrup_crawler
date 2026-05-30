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
    /// Search documents by term using the TF-IDF index.
    /// </summary>
    /// <param name="q">Search term</param>
    /// <param name="page">Page number (starts at 1)</param>
    /// <param name="pageSize">Items per page (max 50)</param>
    /// <param name="startDate">Start date filter (yyyy-MM-dd)</param>
    /// <param name="endDate">End date filter (yyyy-MM-dd)</param>
    /// <param name="sortBy">Relevance (default) or Latest</param>
    [HttpGet]
    public async Task<ActionResult<SearchResponse>> Get(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] SortOrder sortBy = SortOrder.Relevance,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(1, page);

        _logger.LogInformation(
            "Search: '{Term}' | page {Page} | sort {Sort} | range {Start}~{End}",
            q, page, sortBy, startDate?.ToString("yyyy-MM-dd"), endDate?.ToString("yyyy-MM-dd"));

        var filters = new SearchFilters(startDate, endDate, sortBy);
        var result = await _searchService.SearchAsync(q, page, pageSize, filters, ct);
        return Ok(result);
    }
}