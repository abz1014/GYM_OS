using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Attendance;
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

        var record = new AttendanceRecord
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            MemberId = request.MemberId,
            CheckInAt = dateTimeProvider.UtcNow,
            Method = request.Method,
            RecordedByUserId = request.Method == AttendanceMethod.Manual ? currentUser.UserId : null
        };

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
