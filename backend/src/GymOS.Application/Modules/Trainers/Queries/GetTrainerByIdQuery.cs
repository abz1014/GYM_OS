using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Trainers.Dtos;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Queries;

public record GetTrainerByIdQuery(Guid Id) : IQuery<TrainerDetailDto>;

public class GetTrainerByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTrainerByIdQuery, TrainerDetailDto>
{
    public async Task<TrainerDetailDto> Handle(GetTrainerByIdQuery request, CancellationToken cancellationToken)
    {
        var trainer = await db.Trainers.AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Schedules)
            .Include(t => t.Assignments).ThenInclude(a => a.Member)
            .Include(t => t.Ratings).ThenInclude(r => r.Member)
            .Include(t => t.CommissionRecords)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Trainer), request.Id);

        return new TrainerDetailDto(
            trainer.Id, trainer.User?.FirstName ?? string.Empty, trainer.User?.LastName ?? string.Empty,
            trainer.User?.Email ?? string.Empty, trainer.Specialties, trainer.CommissionRate, trainer.Bio,
            trainer.IsActive, trainer.BranchId,
            trainer.Schedules
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new TrainerScheduleDto(s.Id, s.DayOfWeek, s.StartTime, s.EndTime, s.IsAvailable))
                .ToList(),
            trainer.Assignments
                .OrderByDescending(a => a.StartDate)
                .Select(a => new TrainerAssignmentDto(
                    a.Id, a.MemberId, $"{a.Member?.FirstName} {a.Member?.LastName}".Trim(), a.StartDate, a.EndDate, a.IsActive))
                .ToList(),
            trainer.Ratings
                .OrderByDescending(r => r.RatedAt)
                .Select(r => new TrainerRatingDto(
                    r.Id, r.MemberId, $"{r.Member?.FirstName} {r.Member?.LastName}".Trim(), r.Score, r.Comment, r.RatedAt))
                .ToList(),
            trainer.CommissionRecords
                .OrderByDescending(c => c.Period)
                .Select(c => new CommissionRecordDto(c.Id, c.Amount, c.Period, c.Status.ToString()))
                .ToList(),
            trainer.CommissionRecords.Sum(c => c.Amount));
    }
}
