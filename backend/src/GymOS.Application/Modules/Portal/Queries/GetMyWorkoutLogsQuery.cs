using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Application.Modules.Workouts.Queries;
using MediatR;

namespace GymOS.Application.Modules.Portal.Queries;

public record GetMyWorkoutLogsQuery : IQuery<List<WorkoutLogDto>>;

public class GetMyWorkoutLogsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<GetMyWorkoutLogsQuery, List<WorkoutLogDto>>
{
    public async Task<List<WorkoutLogDto>> Handle(GetMyWorkoutLogsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        return await sender.Send(new GetMemberWorkoutLogsQuery(memberId), cancellationToken);
    }
}
