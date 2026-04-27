using Microsoft.AspNetCore.Mvc;
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.Controllers;

[ApiController]
[Route("api/stories")]
internal sealed class StoriesController : ControllerBase
{
    private readonly IStoryCache _cache;

    public StoriesController(IStoryCache cache)
    {
        _cache = cache;
    }

    [HttpGet]
    public ActionResult<StoriesResponse> Get(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) return BadRequest(new { error = "page must be >= 1" });
        if (pageSize < 1 || pageSize > 100) return BadRequest(new { error = "pageSize must be between 1 and 100" });

        return Ok(_cache.Query(search, page, pageSize));
    }
}
