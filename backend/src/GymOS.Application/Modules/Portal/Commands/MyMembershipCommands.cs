using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// A member pausing their own membership.
///
/// Carries no membership id and no member id: which membership is resolved server-side by
/// MyMembershipResolver, and the work is then handed straight to the staff FreezeMembershipCommand.
/// That delegation is the whole point. Freezing is governed by rules that took a live-data incident
/// to get right — only an Active membership may be paused, the plan's allowance is cumulative
/// across every freeze the membership has ever had, and a window that is already over may not be
/// claimed retroactively (see MembershipFreezePolicy). Reimplementing any of that here would give
/// the member a second, laxer door into the same state machine, and the member-facing door is
/// exactly the one an allowance exists to bound.
/// </summary>
public record FreezeMyMembershipCommand(DateOnly FreezeStartDate, DateOnly FreezeEndDate) : ICommand<Unit>;

public class FreezeMyMembershipCommandValidator : AbstractValidator<FreezeMyMembershipCommand>
{
    public FreezeMyMembershipCommandValidator()
        => RuleFor(x => x.FreezeEndDate).GreaterThanOrEqualTo(x => x.FreezeStartDate);
}

public class FreezeMyMembershipCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<FreezeMyMembershipCommand, Unit>
{
    public async Task<Unit> Handle(FreezeMyMembershipCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var membershipId = await MyMembershipResolver.ResolveCurrentMembershipIdAsync(db, memberId, cancellationToken);

        return await sender.Send(
            new FreezeMembershipCommand(membershipId, request.FreezeStartDate, request.FreezeEndDate), cancellationToken);
    }
}

/// <summary>
/// A member restarting their own frozen membership. Delegates to ResumeMembershipCommand so the
/// credit added back to EndDate is the time the membership was ACTUALLY paused — the calculation
/// that stops a freeze/resume cycle minting membership out of nothing.
/// </summary>
public record ResumeMyMembershipCommand : ICommand<Unit>;

public class ResumeMyMembershipCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<ResumeMyMembershipCommand, Unit>
{
    public async Task<Unit> Handle(ResumeMyMembershipCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var membershipId = await MyMembershipResolver.ResolveCurrentMembershipIdAsync(db, memberId, cancellationToken);

        return await sender.Send(new ResumeMembershipCommand(membershipId), cancellationToken);
    }
}

/// <summary>
/// The member turning their own renewal on or off.
///
/// Written directly rather than delegated, because no staff command owns this flag — it is set at
/// signup and renewal and nothing since has been able to change it. That absence is what made
/// auto-renew feel like a trap: the member could see "renews automatically" on their profile and had
/// no way to stop it except phoning the gym.
///
/// Turning it OFF also leaves a note on the member's timeline. A silent flag flip is invisible to
/// the people whose job it is to keep the member — by the time the renewal simply fails to happen,
/// the conversation that might have saved it is a month too late. The note is authored by the
/// member's own user id, so the timeline reads "the member did this", not "someone at the desk did".
/// </summary>
public record SetMyAutoRenewCommand(bool Enabled) : ICommand<Unit>;

public class SetMyAutoRenewCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetMyAutoRenewCommand, Unit>
{
    internal const string TurnedOffNote = "Turned off auto-renew from the member portal.";

    public async Task<Unit> Handle(SetMyAutoRenewCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var membershipId = await MyMembershipResolver.ResolveCurrentMembershipIdAsync(db, memberId, cancellationToken);

        var membership = await db.MemberMemberships.FirstOrDefaultAsync(mm => mm.Id == membershipId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberMembership), membershipId);

        // Only an actual turn-off is worth telling staff about. Without this guard a double-tapped
        // toggle, or a client that re-sends its current state on load, would stack identical
        // "turned off auto-renew" notes on the member's timeline until the real history was buried.
        var turningOff = membership.AutoRenew && !request.Enabled;

        membership.AutoRenew = request.Enabled;

        if (turningOff)
        {
            db.MemberNotes.Add(new MemberNote
            {
                MemberId = memberId,
                Note = TurnedOffNote,
                AuthorUserId = currentUser.UserId!.Value,
                CreatedAt = dateTimeProvider.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// A member asking to cancel — and nothing more than asking.
///
/// This deliberately does NOT change the membership. Cancellation is a decision with money, notice
/// periods and a retention conversation attached to it, and a self-service button that executed it
/// would be the one irreversible action in the portal, reachable by mis-tap. What the member
/// actually lacked was a way to be HEARD: the request used to have to travel by phone call or front
/// desk visit, and mostly did not travel at all.
///
/// So the request is written as a note on the member's timeline — the place staff already look when
/// they open a member — authored by the member's own user id so it is unambiguous who asked.
/// </summary>
public record RequestMyCancellationCommand(string Reason) : ICommand<Unit>;

public class RequestMyCancellationCommandValidator : AbstractValidator<RequestMyCancellationCommand>
{
    public RequestMyCancellationCommandValidator()
        => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class RequestMyCancellationCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RequestMyCancellationCommand, Unit>
{
    internal const string NotePrefix = "Requested cancellation from the member portal: ";

    public async Task<Unit> Handle(RequestMyCancellationCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        // MemberNote, never MedicalNote — an operational request must not land in health data. See
        // the MemberNote entity for why that separation is load-bearing.
        db.MemberNotes.Add(new MemberNote
        {
            MemberId = memberId,
            Note = NotePrefix + request.Reason.Trim(),
            AuthorUserId = currentUser.UserId!.Value,
            CreatedAt = dateTimeProvider.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
