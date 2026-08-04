using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Attendance.Commands;

/// <summary>
/// Records a historical check-in from a migrated legacy system at its original timestamp —
/// deliberately side-effect-free (no live dashboard notification, no "recorded by" staff
/// attribution) unlike CheckInCommand, which is the real-time front-desk check-in flow. A bulk
/// historical import firing hundreds of "someone just checked in!" live notifications would
/// actively mislead staff watching the dashboard, and no staff member was really at the desk for
/// the ones we're backfilling.
/// </summary>
public record ImportAttendanceRecordCommand(
    Guid MemberId, Guid BranchId, DateTimeOffset CheckInAt, DateTimeOffset? CheckOutAt) : ICommand<Guid>;

public class ImportAttendanceRecordCommandValidator : AbstractValidator<ImportAttendanceRecordCommand>
{
    public ImportAttendanceRecordCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CheckOutAt).GreaterThanOrEqualTo(x => x.CheckInAt).When(x => x.CheckOutAt is not null);
    }
}

public class ImportAttendanceRecordCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<ImportAttendanceRecordCommand, Guid>
{
    public async Task<Guid> Handle(ImportAttendanceRecordCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        var record = new AttendanceRecord
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            MemberId = request.MemberId,
            CheckInAt = request.CheckInAt,
            CheckOutAt = request.CheckOutAt,
            Method = AttendanceMethod.Manual,
            RecordedByUserId = null
        };

        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return record.Id;
    }
}
