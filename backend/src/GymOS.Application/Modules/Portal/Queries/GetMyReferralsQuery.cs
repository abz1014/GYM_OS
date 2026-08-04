using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The member's own referral picture: their member code (what a friend gives the front desk so the
/// referral gets attributed) and the friends they've already brought in. First names only on the
/// referred list — enough for "your friends", without turning the portal into a member directory.
/// </summary>
public record GetMyReferralsQuery : IQuery<MyReferralsDto>;

public class GetMyReferralsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyReferralsQuery, MyReferralsDto>
{
    public async Task<MyReferralsDto> Handle(GetMyReferralsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var memberCode = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => m.MemberCode)
            .FirstAsync(cancellationToken);

        var referred = await db.Members.AsNoTracking()
            .Where(m => m.ReferredByMemberId == memberId)
            .OrderBy(m => m.JoinDate)
            .Select(m => new MyReferredMemberDto(m.FirstName, m.JoinDate))
            .ToListAsync(cancellationToken);

        return new MyReferralsDto(memberCode, referred.Count, referred);
    }
}
