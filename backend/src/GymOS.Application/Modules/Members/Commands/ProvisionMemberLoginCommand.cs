using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>
/// The retrofit half of member login: one button, usable on every member, that always leaves them
/// able to sign in.
///
/// A member registered before CreateMemberCommand provisioned an account — every member who existed
/// prior to this feature, plus anyone imported through Migration Center — has <c>UserId == null</c>
/// and no way in short of a developer touching the database. Those members get a brand-new account
/// here. A member who already has one but has lost or forgotten the password gets the same handover
/// ResetStaffPasswordCommand gives staff: a fresh temporary password, and every live session ended so
/// the reset actually revokes what it claims to.
/// </summary>
public record ProvisionMemberLoginCommand(Guid MemberId) : ICommand<CreateMemberResultDto>;

public class ProvisionMemberLoginCommandValidator : AbstractValidator<ProvisionMemberLoginCommand>
{
    public ProvisionMemberLoginCommandValidator() => RuleFor(x => x.MemberId).NotEmpty();
}

public class ProvisionMemberLoginCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IPasswordHasher passwordHasher, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ProvisionMemberLoginCommand, CreateMemberResultDto>
{
    public async Task<CreateMemberResultDto> Handle(ProvisionMemberLoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken)
            ?? throw new NotFoundException(nameof(Member), request.MemberId);

        if (member.UserId is not null)
        {
            return await ResetExistingLoginAsync(member, cancellationToken);
        }

        var (user, temporaryPassword) = await MemberLoginProvisioner.ProvisionAsync(
            db, passwordHasher, tenantId, member.BranchId, member.Email,
            member.FirstName, member.LastName, member.Phone, cancellationToken);

        member.UserId = user.Id;
        await db.SaveChangesAsync(cancellationToken);

        return new CreateMemberResultDto(member.Id, temporaryPassword);
    }

    private async Task<CreateMemberResultDto> ResetExistingLoginAsync(Member member, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == member.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), member.UserId!.Value);

        var temporaryPassword = TokenHasher.GenerateTemporaryPassword();
        user.PasswordHash = passwordHasher.Hash(temporaryPassword);

        // Same reasoning as ResetStaffPasswordCommand: a reset that leaves the old refresh token alive
        // protects nothing, since whoever holds it keeps rotating in fresh access tokens regardless.
        var liveTokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in liveTokens)
        {
            token.RevokedAt = dateTimeProvider.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new CreateMemberResultDto(member.Id, temporaryPassword);
    }
}
