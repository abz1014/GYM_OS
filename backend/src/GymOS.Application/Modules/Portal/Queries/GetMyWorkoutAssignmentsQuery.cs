using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Application.Modules.Workouts.Queries;
using MediatR;

namespace GymOS.Application.Modules.Portal.Queries;

public record GetMyWorkoutAssignmentsQuery : IQuery<List<WorkoutAssignmentListItemDto>>;

public class GetMyWorkoutAssignmentsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<GetMyWorkoutAssignmentsQuery, List<WorkoutAssignmentListItemDto>>
{
    public async Task<List<WorkoutAssignmentListItemDto>> Handle(GetMyWorkoutAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        return await sender.Send(new GetMemberWorkoutAssignmentsQuery(memberId), cancellationToken);
    }
}
