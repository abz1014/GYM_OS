using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Search.Dtos;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Search.Queries;

/// <summary>
/// One box, everything the caller is allowed to find. Backs the ⌘K palette.
/// </summary>
public record GlobalSearchQuery(string? Term, int PerGroupLimit = 5) : IQuery<GlobalSearchResultDto>;

/// <summary>
/// The search behind the palette.
///
/// Two rules decide the whole shape of this handler.
///
/// **Every group is gated on its own permission, and the endpoint itself needs none.** A single
/// `search.view` permission would have been simpler and wrong: it would either hand a Nutritionist
/// the invoice list or deny the Accountant a search box entirely. Instead the caller gets exactly
/// the groups their existing module permissions already allow, which means the palette can be shown
/// unconditionally to every staff member without a per-role audit — the worst case for a role with
/// no search-able modules is three empty groups, not a leak.
///
/// **Branch scope is applied identically to the module lists.** This endpoint reaches across
/// modules, which makes it exactly the kind of place the earlier branch-leak happened: a
/// Receptionist with access to one branch must not find another branch's members by typing a name.
/// Members and Invoices are branch-scoped through <see cref="BranchAccessResolver"/>. ClassTypes are
/// tenant-scoped by design (a class type is a catalogue entry, not a per-branch record), so they
/// ride the DbContext's global tenant filter and take no branch predicate.
///
/// Results are capped per group rather than paged. A palette is for finding one known thing, not
/// for browsing; if the answer isn't in the first few rows the person should type more, and the
/// module lists already exist for real browsing.
/// </summary>
public class GlobalSearchQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GlobalSearchQuery, GlobalSearchResultDto>
{
    /// <summary>
    /// Below this the result set is noise — one or two characters match most of the database and
    /// return a list nobody can use, at the cost of three table scans per keystroke.
    /// </summary>
    private const int MinimumTermLength = 2;

    public async Task<GlobalSearchResultDto> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var term = request.Term?.Trim();
        if (string.IsNullOrEmpty(term) || term.Length < MinimumTermLength)
        {
            return new GlobalSearchResultDto([], [], []);
        }

        // Clamped rather than validated-and-rejected: the limit is a display concern, and a palette
        // asking for 500 rows is a client bug that should degrade, not 400 in someone's face.
        var limit = Math.Clamp(request.PerGroupLimit, 1, 20);

        /*
         * Lower-cased on both sides, because `Contains` translates to a case-SENSITIVE LIKE on
         * PostgreSQL: typing "yoga" found nothing while "Yoga" found the class, which is not a
         * search box, it is a guessing game. Caught by testing the endpoint rather than by reading
         * it — the code looks correct.
         *
         * `ToLower()` rather than Npgsql's `EF.Functions.ILike` on purpose: ILike does not translate
         * on SQLite, which is the provider the application test suite runs against, so the version
         * under test would not have been the version that ships.
         *
         * This costs the index on these columns. At the scale a single gym's search box operates on,
         * capped at 20 rows per group and gated behind a two-character minimum, that is the right
         * trade; a citext column or a functional index is the answer if it ever stops being.
         */
        var needle = term.ToLowerInvariant();

        var members = await SearchMembersAsync(term, needle, limit, cancellationToken);
        var invoices = await SearchInvoicesAsync(needle, limit, cancellationToken);
        var classes = await SearchClassesAsync(needle, limit, cancellationToken);

        return new GlobalSearchResultDto(members, invoices, classes);
    }

    private async Task<List<SearchHitDto>> SearchMembersAsync(string term, string needle, int limit, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionCodes.Members.View))
        {
            return [];
        }

        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

        // Same match rule as GetMembersListQuery, deliberately: the front desk searching the members
        // list and the same person searching the palette must not get different answers for the same
        // string. That includes the exact-match QR token, so a scanner fired at the palette works.
        return await db.Members.AsNoTracking()
            .Where(m => accessibleBranchIds.Contains(m.BranchId))
            .Where(m =>
                m.FirstName.ToLower().Contains(needle) || m.LastName.ToLower().Contains(needle) ||
                m.Email.ToLower().Contains(needle) || m.MemberCode.ToLower().Contains(needle) ||
                // The token stays an EXACT, case-sensitive match: it is opaque and machine-emitted,
                // so loosening it only risks colliding with the middle of somebody else's token.
                m.QrCodeToken == term)
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Take(limit)
            .Select(m => new SearchHitDto(
                m.Id,
                m.FirstName + " " + m.LastName,
                m.MemberCode,
                "/members/" + m.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SearchHitDto>> SearchInvoicesAsync(string needle, int limit, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionCodes.Billing.View))
        {
            return [];
        }

        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

        // Invoice number OR the member's name: staff are handed a paper invoice as often as they are
        // told "Sarah says she already paid", and both have to land somewhere.
        return await db.Invoices.AsNoTracking()
            .Where(i => accessibleBranchIds.Contains(i.BranchId))
            .Where(i =>
                i.InvoiceNumber.ToLower().Contains(needle) ||
                (i.Member != null && (i.Member.FirstName.ToLower().Contains(needle) || i.Member.LastName.ToLower().Contains(needle))))
            .OrderByDescending(i => i.IssueDate)
            .Take(limit)
            .Select(i => new SearchHitDto(
                i.Id,
                i.InvoiceNumber,
                i.Member == null ? null : i.Member.FirstName + " " + i.Member.LastName,
                "/billing/" + i.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SearchHitDto>> SearchClassesAsync(string needle, int limit, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionCodes.Classes.View))
        {
            return [];
        }

        // Every class hit routes to the list, not to a detail page, because no such route exists —
        // there is no /classes/:id in the router. The subtitle says so rather than letting the row
        // imply a jump it cannot make. When a class detail route lands, only this projection changes.
        return await db.ClassTypes.AsNoTracking()
            .Where(c => c.IsActive)
            .Where(c => c.Name.ToLower().Contains(needle))
            .OrderBy(c => c.Name)
            .Take(limit)
            .Select(c => new SearchHitDto(c.Id, c.Name, "Open the class schedule", "/classes"))
            .ToListAsync(cancellationToken);
    }
}
