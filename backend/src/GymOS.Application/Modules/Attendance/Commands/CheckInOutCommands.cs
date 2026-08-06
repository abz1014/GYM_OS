using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Attendance;
using GymOS.Domain.Common;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Attendance.Commands;

/// <summary>
/// "QR check-in" is simulated for the MVP — no camera/QR-decode, just resolving the member and
/// stamping a check-in the same way a real scan would. Real door-access/biometric check-in
/// methods plug in later behind IDoorAccessProvider without changing this handler.
/// </summary>
public record CheckInCommand(Guid MemberId, Guid BranchId, AttendanceMethod Method) : ICommand<Guid>;

public class CheckInCommandValidator : AbstractValidator<CheckInCommand>
{
    public CheckInCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public class CheckInCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider, IDashboardNotifier dashboardNotifier)
    : IRequestHandler<CheckInCommand, Guid>
{
    public async Task<Guid> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        /*
         * Someone already in the building does not get a second visit.
         *
         * Nothing stopped that before, and it is the easiest thing in the world to trigger: a counter
         * scanner firing twice on one swipe, a member scanning again because they didn't see the
         * screen change, a receptionist pressing the button twice. Every duplicate opened another row
         * with no check-out, so the "in the building" count drifted upward all day and never came
         * back down — a desk was being told the room held more people than had walked through it.
         *
         * Returning the visit they are already on rather than throwing: a scanner double-firing is
         * not an error anybody needs to see, and the caller gets the same shape either way. This is
         * the same choice LogMyRecoveryCommand makes for a second same-day recovery log.
         *
         * Branch-scoped because branch is deliberately NOT a global query filter (see
         * GymOsDbContext.ApplyGlobalQueryFilters) — a member genuinely can train at two sites, and
         * being mid-visit at one says nothing about arriving at the other.
         */
        var zone = GymDay.ZoneOrUtc(await db.Branches.AsNoTracking()
            .Where(b => b.Id == request.BranchId)
            .Select(b => b.TimeZone)
            .FirstOrDefaultAsync(cancellationToken));

        var openVisits = await db.AttendanceRecords
            .Where(a => a.MemberId == request.MemberId && a.BranchId == request.BranchId && a.CheckOutAt == null)
            .Select(a => new { a.Id, a.CheckInAt })
            .ToListAsync(cancellationToken);

        // The date comparison runs in memory: a DateTimeOffset cannot be bucketed to a date in SQL on
        // SQLite, the same constraint every query over these rows works around.
        var alreadyInside = openVisits
            .FirstOrDefault(v => VisitPolicy.IsInsideNow(v.CheckInAt, null, dateTimeProvider.UtcNow, zone));
        if (alreadyInside is not null)
        {
            return alreadyInside.Id;
        }

        var record = new AttendanceRecord
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            MemberId = request.MemberId,
            CheckInAt = dateTimeProvider.UtcNow,
            Method = request.Method,
            RecordedByUserId = request.Method == AttendanceMethod.Manual ? currentUser.UserId : null
        };

        record.RaiseCheckedIn();

        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        await dashboardNotifier.NotifyBranchActivityAsync(request.BranchId, "check-in", cancellationToken);

        return record.Id;
    }
}

public record CheckOutCommand(Guid AttendanceRecordId) : ICommand<Unit>;

public class CheckOutCommandValidator : AbstractValidator<CheckOutCommand>
{
    public CheckOutCommandValidator() => RuleFor(x => x.AttendanceRecordId).NotEmpty();
}

public class CheckOutCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CheckOutCommand, Unit>
{
    public async Task<Unit> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.Id == request.AttendanceRecordId, cancellationToken)
            ?? throw new NotFoundException(nameof(AttendanceRecord), request.AttendanceRecordId);

        record.CheckOutAt = dateTimeProvider.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
