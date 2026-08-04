using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Classes;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Commands;

/// <summary>
/// Books a member into a session. Confirms the spot if capacity remains, otherwise waitlists them
/// (ClassBookingPolicy decides). Refuses to double-book a member who already holds an active place,
/// and refuses sessions that aren't open (cancelled or already run). Returns the resulting status so
/// the caller can tell the member "you're in" vs "you're on the waitlist".
/// </summary>
public record BookClassSessionCommand(Guid ClassSessionId, Guid MemberId) : ICommand<ClassBookingStatus>;

public class BookClassSessionCommandValidator : AbstractValidator<BookClassSessionCommand>
{
    public BookClassSessionCommandValidator()
    {
        RuleFor(x => x.ClassSessionId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

public class BookClassSessionCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<BookClassSessionCommand, ClassBookingStatus>
{
    public async Task<ClassBookingStatus> Handle(BookClassSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == request.ClassSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClassSession), request.ClassSessionId);

        if (session.Status != ClassSessionStatus.Scheduled)
        {
            throw new ValidationException("This session is not open for booking.");
        }

        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Member), request.MemberId);
        }

        var alreadyBooked = await db.ClassBookings.AnyAsync(
            b => b.ClassSessionId == session.Id && b.MemberId == request.MemberId
                 && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted || b.Status == ClassBookingStatus.CheckedIn),
            cancellationToken);
        if (alreadyBooked)
        {
            throw new ValidationException("This member already has an active booking for this session.");
        }

        var occupied = await db.ClassBookings.CountAsync(
            b => b.ClassSessionId == session.Id && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.CheckedIn),
            cancellationToken);

        var status = ClassBookingPolicy.StatusForNewBooking(occupied, session.Capacity);

        db.ClassBookings.Add(new ClassBooking
        {
            TenantId = session.TenantId,
            BranchId = session.BranchId,
            ClassSessionId = session.Id,
            MemberId = request.MemberId,
            Status = status,
            BookedAt = dateTimeProvider.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        return status;
    }
}
