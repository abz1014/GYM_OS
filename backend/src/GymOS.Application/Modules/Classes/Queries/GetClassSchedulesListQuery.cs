using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Classes.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Queries;

public record GetClassSchedulesListQuery(Guid? BranchId) : IQuery<List<ClassScheduleDto>>;

public class GetClassSchedulesListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetClassSchedulesListQuery, List<ClassScheduleDto>>
{
    public async Task<List<ClassScheduleDto>> Handle(GetClassSchedulesListQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.ClassSchedules.AsNoTracking()
            .Include(s => s.ClassType)
            .Include(s => s.Trainer!).ThenInclude(t => t.User)
            .Where(s => accessibleBranchIds.Contains(s.BranchId));

        if (request.BranchId is not null)
        {
            query = query.Where(s => s.BranchId == request.BranchId);
        }

        return await query
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .Select(s => new ClassScheduleDto(
                s.Id, s.ClassTypeId, s.ClassType!.Name, s.ClassType.ColorHex,
                s.TrainerId, s.Trainer == null ? null : s.Trainer.User!.FirstName + " " + s.Trainer.User.LastName,
                s.DayOfWeek, s.StartTime, s.DurationMinutes, s.Capacity, s.Location, s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
