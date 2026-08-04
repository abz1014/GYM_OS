using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

public record CreateMemberCommand(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    Guid BranchId,
    Guid? ReferredByMemberId = null) : ICommand<Guid>;

public class CreateMemberCommandValidator : AbstractValidator<CreateMemberCommand>
{
    public CreateMemberCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public class CreateMemberCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateMemberCommand, Guid>
{
    public async Task<Guid> Handle(CreateMemberCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var branchExists = await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException(nameof(Domain.Tenancy.Branch), request.BranchId);
        }

        // The referrer must be a real member of this tenant — the global query filter makes a
        // cross-tenant id indistinguishable from a nonexistent one, which is exactly right.
        if (request.ReferredByMemberId is not null
            && !await db.Members.AnyAsync(m => m.Id == request.ReferredByMemberId, cancellationToken))
        {
            throw new NotFoundException(nameof(Member), request.ReferredByMemberId);
        }

        var sequence = await db.Members.IgnoreQueryFilters().CountAsync(m => m.TenantId == tenantId, cancellationToken) + 1;

        var member = new Member
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            MemberCode = $"MBR-{sequence:D5}",
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address,
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N"),
            ReferredByMemberId = request.ReferredByMemberId
        };

        db.Members.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        return member.Id;
    }
}
