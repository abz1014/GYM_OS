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
            /*
             * The QR token is matched EXACTLY, everything else by substring.
             *
             * Every member has carried a QrCodeToken since they were created, and the attendance
             * method is literally called QrSimulated — but nothing ever searched by it, so a real
             * scanner emitting a member's token found nobody and the loop was never closed. Adding it
             * here rather than behind a second endpoint means the front desk keeps ONE input: a name,
             * a code, an email and a scanned card all land in the same box, which is the only design
             * that works when the thing typing is a barcode reader.
             *
             * Exact, not Contains, for two reasons. A token is opaque and 32 hex characters, so a
             * substring match buys nothing a human would ever type; and a short accidental term could
             * otherwise collide with the middle of somebody's token and return a member the person
             * searching has no business being shown.
             */
            query = query.Where(m =>
                m.FirstName.Contains(term) || m.LastName.Contains(term) ||
                m.Email.Contains(term) || m.MemberCode.Contains(term) ||
                m.QrCodeToken == term);
        }

        var projected = query
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Select(m => new MemberListItemDto(
                m.Id, m.MemberCode, m.FirstName + " " + m.LastName, m.Email, m.Phone,
                m.ProfilePhotoUrl, m.Status, m.JoinDate));

        return await projected.ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
    }
}
