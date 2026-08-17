using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// Where this member trains and how to contact the people who run it.
///
/// The defect this closes is a small one that ran through the whole product: half a dozen
/// member-facing strings tell the member to "ask the front desk" or "contact us", and the app had no
/// endpoint that would say where the desk was or what number to call. Every one of those sentences
/// was a dead end. The branch is resolved from the member's own record — there is no branch id
/// parameter, so this cannot become a directory of every site the tenant operates.
/// </summary>
public record GetMyGymQuery : IQuery<MyGymDto>;

public class GetMyGymQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyGymQuery, MyGymDto>
{
    public async Task<MyGymDto> Handle(GetMyGymQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var branch = await (from m in db.Members.AsNoTracking()
                            join b in db.Branches.AsNoTracking() on m.BranchId equals b.Id
                            where m.Id == memberId
                            select new { b.Name, b.AddressLine, b.City, b.Country })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), memberId);

        /*
         * Support contacts live on the tenant's GymProfile, not on Branch — one gym, one number,
         * however many sites. Both fields are nullable there and stay nullable here rather than
         * collapsing to "": a gym that has not filled in a support phone must render as having no
         * phone, not as a tel: link to nowhere. A tenant with no profile row at all (fresh install)
         * is the same situation, so it takes the same path instead of throwing.
         */
        var profile = await db.GymProfiles.AsNoTracking()
            .Select(p => new { p.SupportEmail, p.SupportPhone })
            .FirstOrDefaultAsync(cancellationToken);

        return new MyGymDto(
            branch.Name, branch.AddressLine, branch.City, branch.Country,
            profile?.SupportEmail, profile?.SupportPhone);
    }
}
