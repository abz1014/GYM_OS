using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Trainers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Queries;

public record GetTrainersListQuery(Guid? BranchId) : IQuery<List<TrainerListItemDto>>;

public class GetTrainersListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTrainersListQuery, List<TrainerListItemDto>>
{
    public Task<List<TrainerListItemDto>> Handle(GetTrainersListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Trainers.AsNoTracking().Include(t => t.User).AsQueryable();

        if (request.BranchId is not null)
        {
            query = query.Where(t => t.BranchId == request.BranchId);
        }

        return query
            .OrderBy(t => t.User!.FirstName)
            .Select(t => new TrainerListItemDto(
                t.Id,
                t.User!.FirstName + " " + t.User.LastName,
                t.User.Email,
                t.Specialties,
                t.CommissionRate,
                t.IsActive,
                t.Assignments.Count(a => a.IsActive),
                t.Ratings.Any() ? t.Ratings.Average(r => r.Score) : (double?)null))
            .ToListAsync(cancellationToken);
    }
}
