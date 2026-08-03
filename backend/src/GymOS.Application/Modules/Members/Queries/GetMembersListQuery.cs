using GymOS.Application.Common.Extensions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Domain.Members;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Queries;

public record GetMembersListQuery(string? SearchTerm, MemberStatus? Status, Guid? BranchId, int Page = 1, int PageSize = 20)
    : IQuery<PagedList<MemberListItemDto>>;

public class GetMembersListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMembersListQuery, PagedList<MemberListItemDto>>
{
    public async Task<PagedList<MemberListItemDto>> Handle(GetMembersListQuery request, CancellationToken cancellationToken)
    {
        // The exact gap that started this security review: omitting BranchId here returned
        // every branch's members to any caller holding members.view, including a Receptionist
        // whose UserBranchAccess only ever granted them one branch.
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.Members.AsNoTracking().Where(m => accessibleBranchIds.Contains(m.BranchId));

        if (request.BranchId is not null)
        {
            query = query.Where(m => m.BranchId == request.BranchId);
        }

        if (request.Status is not null)
        {
            query = query.Where(m => m.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(m =>
                m.FirstName.Contains(term) || m.LastName.Contains(term) ||
                m.Email.Contains(term) || m.MemberCode.Contains(term));
        }

        var projected = query
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Select(m => new MemberListItemDto(
                m.Id, m.MemberCode, m.FirstName + " " + m.LastName, m.Email, m.Phone,
                m.ProfilePhotoUrl, m.Status, m.JoinDate));

        return await projected.ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
    }
}
