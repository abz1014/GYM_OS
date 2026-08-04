using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Classes;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Commands;

/// <summary>
/// Creates a recurring weekly class slot. DurationMinutes/Capacity are optional — when omitted they
/// inherit the ClassType's defaults, so "add Spin on Mondays" needs nothing more than the type, day,
/// and time. On creation the first booking window of concrete sessions is generated immediately, so
/// the calendar is populated without waiting for the nightly generation job.
/// </summary>
public record CreateClassScheduleCommand(
    Guid BranchId, Guid ClassTypeId, Guid? TrainerId, DayOfWeek DayOfWeek, TimeOnly StartTime,
    int? DurationMinutes, int? Capacity, string? Location) : ICommand<Guid>;

public class CreateClassScheduleCommandValidator : AbstractValidator<CreateClassScheduleCommand>
{
    public CreateClassScheduleCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480).When(x => x.DurationMinutes is not null);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 1000).When(x => x.Capacity is not null);
    }
}

public class CreateClassScheduleCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateClassScheduleCommand, Guid>
{
    public async Task<Guid> Handle(CreateClassScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var classType = await db.ClassTypes.FirstOrDefaultAsync(t => t.Id == request.ClassTypeId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClassType), request.ClassTypeId);

        if (request.TrainerId is not null)
        {
            var trainerExists = await db.Trainers.AnyAsync(t => t.Id == request.TrainerId, cancellationToken);
            if (!trainerExists)
            {
                throw new NotFoundException(nameof(Trainer), request.TrainerId.Value);
            }
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var throughDate = today.AddDays(ClassSessionPlanner.DefaultWindowDays);

        var schedule = new ClassSchedule
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            ClassTypeId = request.ClassTypeId,
            TrainerId = request.TrainerId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            DurationMinutes = request.DurationMinutes ?? classType.DefaultDurationMinutes,
            Capacity = request.Capacity ?? classType.DefaultCapacity,
            Location = request.Location,
            IsActive = true,
            GeneratedThroughDate = throughDate
        };

        db.ClassSchedules.Add(schedule);

        var sessions = ClassSessionPlanner.BuildSessions(schedule, today, throughDate, new HashSet<DateOnly>());
        db.ClassSessions.AddRange(sessions);

        await db.SaveChangesAsync(cancellationToken);

        return schedule.Id;
    }
}
