using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Classes.Commands;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// A member cancelling their own booking (or leaving a waitlist). The ownership check here is the
/// security boundary — a member must not be able to cancel anyone else's place — after which it
/// delegates to the shared CancelClassBookingCommand so the waitlist auto-promotion still happens.
/// </summary>
public record CancelMyClassBookingCommand(Guid ClassBookingId) : ICommand<Unit>;

public class CancelMyClassBookingCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<CancelMyClassBookingCommand, Unit>
{
    public async Task<Unit> Handle(CancelMyClassBookingCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var ownerMemberId = await db.ClassBookings.AsNoTracking()
            .Where(b => b.Id == request.ClassBookingId)
            .Select(b => (Guid?)b.MemberId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(ClassBooking), request.ClassBookingId);

        // Not the member's own booking → not found, not "forbidden" (don't confirm it exists).
        if (ownerMemberId != memberId)
        {
            throw new NotFoundException(nameof(ClassBooking), request.ClassBookingId);
        }

        return await sender.Send(new CancelClassBookingCommand(request.ClassBookingId), cancellationToken);
    }
}
