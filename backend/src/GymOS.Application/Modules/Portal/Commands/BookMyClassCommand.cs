using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Classes.Commands;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// A member booking themselves into a class. The member is resolved from the JWT — the caller can
/// only ever book their own place — and the session is verified to belong to the member's own
/// branch before delegating to the shared BookClassSessionCommand, so all the capacity/waitlist/
/// no-double-book rules are applied in exactly one place.
/// </summary>
public record BookMyClassCommand(Guid ClassSessionId) : ICommand<ClassBookingStatus>;

public class BookMyClassCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<BookMyClassCommand, ClassBookingStatus>
{
    public async Task<ClassBookingStatus> Handle(BookMyClassCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var branchId = await db.Members.AsNoTracking().Where(m => m.Id == memberId).Select(m => m.BranchId).FirstAsync(cancellationToken);

        var sessionBranchId = await db.ClassSessions.AsNoTracking()
            .Where(s => s.Id == request.ClassSessionId)
            .Select(s => (Guid?)s.BranchId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(ClassSession), request.ClassSessionId);

        // A member can only book classes at their own branch — hide anything else as not-found.
        if (sessionBranchId != branchId)
        {
            throw new NotFoundException(nameof(ClassSession), request.ClassSessionId);
        }

        return await sender.Send(new BookClassSessionCommand(request.ClassSessionId, memberId), cancellationToken);
    }
}
