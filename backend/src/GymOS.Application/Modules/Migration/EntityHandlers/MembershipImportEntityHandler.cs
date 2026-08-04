using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Domain.Members;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

/// <summary>
/// Imports a membership period against an already-existing member (resolved by email) and plan
/// (resolved by name), rather than the flat create-from-row shape the other handlers use — this is
/// exactly the "resolves a reference to an already-existing entity" case the original
/// GetImportEntitySchemasQuery note flagged as unsupported. Delegates to
/// ImportMemberMembershipCommand rather than the live RenewMembershipCommand — see that command's
/// doc comment for why.
/// </summary>
public class MembershipImportEntityHandler(IApplicationDbContext db) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Membership;

    public IReadOnlyList<string> RequiredFields { get; } = ["MemberEmail", "PlanName", "StartDate", "EndDate", "PricePaid"];

    public IReadOnlyList<string> OptionalFields { get; } = ["Currency", "AutoRenew", "Status"];

    public string? GetNaturalKey(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("MemberEmail", out var email) || string.IsNullOrWhiteSpace(email)
            || !fields.TryGetValue("StartDate", out var startDate) || string.IsNullOrWhiteSpace(startDate))
        {
            return null;
        }

        // A member can have several membership periods over time, so email alone isn't a unique
        // key — pair it with StartDate, the value that actually distinguishes one period from
        // another for the same person.
        return $"{email.Trim().ToLowerInvariant()}|{startDate.Trim()}";
    }

    public async Task<ImportValidationResult> ValidateAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        foreach (var required in RequiredFields)
        {
            if (!fields.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return ImportValidationResult.Invalid($"Missing required field '{required}'.");
            }
        }

        var email = fields["MemberEmail"];
        var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == email, cancellationToken);
        if (member is null)
        {
            return ImportValidationResult.Invalid($"No member found with email '{email}'. Import members first.");
        }

        var planName = fields["PlanName"];
        var plan = await db.MembershipPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Name == planName, cancellationToken);
        if (plan is null)
        {
            return ImportValidationResult.Invalid($"No membership plan found named '{planName}'.");
        }

        if (!DateOnly.TryParse(fields["StartDate"], out var startDate))
        {
            return ImportValidationResult.Invalid($"'{fields["StartDate"]}' is not a valid date for StartDate (expected yyyy-MM-dd).");
        }

        if (!DateOnly.TryParse(fields["EndDate"], out var endDate))
        {
            return ImportValidationResult.Invalid($"'{fields["EndDate"]}' is not a valid date for EndDate (expected yyyy-MM-dd).");
        }

        if (endDate < startDate)
        {
            return ImportValidationResult.Invalid("EndDate cannot be before StartDate.");
        }

        if (!decimal.TryParse(fields["PricePaid"], out var price) || price < 0)
        {
            return ImportValidationResult.Invalid($"'{fields["PricePaid"]}' is not a valid non-negative amount for PricePaid.");
        }

        if (fields.TryGetValue("Status", out var status) && !string.IsNullOrWhiteSpace(status)
            && !Enum.TryParse<MemberMembershipStatus>(status, ignoreCase: true, out _))
        {
            return ImportValidationResult.Invalid($"'{status}' is not a valid membership status.");
        }

        if (fields.TryGetValue("AutoRenew", out var autoRenew) && !string.IsNullOrWhiteSpace(autoRenew) && !bool.TryParse(autoRenew, out _))
        {
            return ImportValidationResult.Invalid($"'{autoRenew}' is not a valid true/false value for AutoRenew.");
        }

        var alreadyExists = await db.MemberMemberships.AnyAsync(
            mm => mm.MemberId == member.Id && mm.MembershipPlanId == plan.Id && mm.StartDate == startDate, cancellationToken);
        if (alreadyExists)
        {
            return ImportValidationResult.Duplicate($"'{email}' already has a '{planName}' membership starting {startDate:yyyy-MM-dd}.");
        }

        return ImportValidationResult.Ok();
    }

    public async Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().FirstAsync(m => m.Email == fields["MemberEmail"], cancellationToken);
        var plan = await db.MembershipPlans.AsNoTracking().FirstAsync(p => p.Name == fields["PlanName"], cancellationToken);

        var startDate = DateOnly.Parse(fields["StartDate"]);
        var endDate = DateOnly.Parse(fields["EndDate"]);
        var price = decimal.Parse(fields["PricePaid"]);

        var currency = fields.TryGetValue("Currency", out var curRaw) && !string.IsNullOrWhiteSpace(curRaw)
            ? curRaw.Trim().ToUpperInvariant()
            : "USD";

        var autoRenew = fields.TryGetValue("AutoRenew", out var ar) && bool.TryParse(ar, out var arParsed) && arParsed;

        var status = fields.TryGetValue("Status", out var s) && !string.IsNullOrWhiteSpace(s) && Enum.TryParse<MemberMembershipStatus>(s, ignoreCase: true, out var parsedStatus)
            ? parsedStatus
            : (endDate >= DateOnly.FromDateTime(DateTime.UtcNow) ? MemberMembershipStatus.Active : MemberMembershipStatus.Expired);

        return await sender.Send(
            new ImportMemberMembershipCommand(member.Id, plan.Id, startDate, endDate, status, price, currency, autoRenew),
            cancellationToken);
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var membership = await db.MemberMemberships.FirstOrDefaultAsync(mm => mm.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberMembership), mappedEntityId);

        membership.Status = MemberMembershipStatus.Cancelled;
        membership.CancellationReason = "Import rolled back";

        await db.SaveChangesAsync(cancellationToken);
    }
}
