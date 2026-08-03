using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Members;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job (registered daily via Hangfire in Program.cs). Closes the workflow's missing
/// "Expiry" step — until this existed, MembershipExpiryCheckJob only ever sent an "expiring soon"
/// notification; nothing ever actually flipped a membership past its EndDate to Expired.
/// </summary>
public class MembershipExpiryTransitionJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<MembershipExpiryTransitionJob> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var expiring = await db.MemberMemberships.IgnoreQueryFilters()
            .Include(mm => mm.Member)
            .Where(mm => mm.Status == MemberMembershipStatus.Active && mm.EndDate < today)
            .ToListAsync(cancellationToken);

        foreach (var membership in expiring)
        {
            membership.Status = MemberMembershipStatus.Expired;

            if (membership.Member is not null && membership.Member.Status == MemberStatus.Active)
            {
                var hasOtherActiveMembership = await db.MemberMemberships.IgnoreQueryFilters().AnyAsync(
                    mm => mm.MemberId == membership.MemberId && mm.Id != membership.Id
                        && (mm.Status == MemberMembershipStatus.Active || mm.Status == MemberMembershipStatus.Frozen),
                    cancellationToken);

                if (!hasOtherActiveMembership)
                {
                    membership.Member.Status = MemberStatus.Expired;
                }
            }
        }

        var updated = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Membership expiry transition flipped {Count} membership(s) to Expired", expiring.Count);
        return updated;
    }
}
