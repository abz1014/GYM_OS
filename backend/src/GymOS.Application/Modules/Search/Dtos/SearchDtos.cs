namespace GymOS.Application.Modules.Search.Dtos;

/// <summary>
/// One thing the palette can jump to. `Route` is resolved server-side rather than assembled in the
/// client so a hit can never point at a page that doesn't exist — the server knows which entities
/// have an addressable detail view and which don't.
/// </summary>
/// <param name="Id">The entity id, so the client can key the row without parsing the route.</param>
/// <param name="Title">What the person typed at — the name, the number.</param>
/// <param name="Subtitle">The line that disambiguates two hits with the same title.</param>
/// <param name="Route">Where selecting this hit navigates. Always a real route.</param>
public record SearchHitDto(Guid Id, string Title, string? Subtitle, string Route);

/// <summary>
/// Grouped rather than one flat ranked list, because the groups are not comparable: an invoice
/// number match is exact and a member name match is fuzzy, so interleaving them by "relevance"
/// would need a scoring model this system has no basis for. Grouping lets the person pick the
/// KIND of thing first, which is how people actually search a business system.
///
/// A group the caller cannot see is absent, not empty — see GlobalSearchQuery.
/// </summary>
public record GlobalSearchResultDto(
    IReadOnlyList<SearchHitDto> Members,
    IReadOnlyList<SearchHitDto> Invoices,
    IReadOnlyList<SearchHitDto> Classes);
