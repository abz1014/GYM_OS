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
        /*
         * Serialise bookings per session. Every check below — "already booked", the occupancy count —
         * reads state that another in-flight booking is about to change, and under load they all
         * passed together: 12 concurrent requests against a capacity-2 session produced 6 confirmed
         * places, and one member ended up holding 3 simultaneous Booked rows for one session (found
         * live, on a member's own screen). An advisory lock keyed on the session makes concurrent
         * bookings for the SAME session queue behind each other, while different sessions stay fully
         * parallel. Transaction-scoped (TransactionBehavior wraps every command), so it releases on
         * commit or rollback with nothing to clean up.
         *
         * Postgres-only by provider check: SQLite (the test harness) runs single-threaded, and the
         * duplicate-booking half of the race is also closed structurally by the partial unique index
         * on active (session, member) rows — the lock is what protects the CAPACITY half.
         */
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            await db.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({request.ClassSessionId.ToString()}, 0))",
                cancellationToken);
        }

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
