using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Application.Modules.Members.Queries;
using MediatR;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>Takes no parameters by design — "whose profile" is resolved server-side from the JWT,
/// not accepted from the caller. Delegates to GetMemberByIdQuery so the response shape (including
/// membership history) stays identical to what staff see on a member's detail page.</summary>
public record GetMyProfileQuery : IQuery<MemberDetailDto>;

public class GetMyProfileQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<GetMyProfileQuery, MemberDetailDto>
{
    public async Task<MemberDetailDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        return await sender.Send(new GetMemberByIdQuery(memberId), cancellationToken);
    }
}
