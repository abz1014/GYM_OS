using GymOS.Application.Modules.Search.Dtos;
using GymOS.Application.Modules.Search.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

/// <summary>
/// The one search box behind the ⌘K palette.
///
/// Deliberately carries no <c>[RequirePermission]</c>. That is not an oversight and not a hole: the
/// handler resolves each result group against the caller's own module permissions and returns an
/// empty group where they hold none, so the effective authorization is exactly the union of what
/// they can already read. A single gate here would have to be either broad enough to leak billing
/// data to a Nutritionist or narrow enough to deny the Accountant a search box — the per-group check
/// is the only rule that is correct for all seven staff roles at once.
///
/// <c>[Authorize]</c> still applies: an anonymous caller gets 401, never results.
/// </summary>
[ApiController]
[Authorize]
[Route("api/search")]
public class SearchController(ISender mediator) : ControllerBase
{
    /// <param name="q">What the person typed. Under two characters returns empty groups, not an error.</param>
    /// <param name="limit">Rows per group, clamped server-side to 1..20.</param>
    [HttpGet]
    public async Task<ActionResult<GlobalSearchResultDto>> Search(
        [FromQuery] string? q, [FromQuery] int limit = 5, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GlobalSearchQuery(q, limit), cancellationToken));
}
