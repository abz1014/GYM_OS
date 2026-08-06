using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Common;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The member's map of their own gym: every movement the gym offers, marked with what they have done
/// on it and what they have not touched.
///
/// The catalogue leads and mastery is joined onto it — the other way round would only ever return
/// what the member has already trained, which is what every existing surface does and is precisely
/// the half that is not interesting here. Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyPassportQuery : IQuery<MyPassportDto>;

public class GetMyPassportQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyPassportQuery, MyPassportDto>
{
    public async Task<MyPassportDto> Handle(GetMyPassportQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        // The gym's whole catalogue, tenant-scoped by the global filter.
        var catalogue = await db.Exercises.AsNoTracking()
            .Select(e => new { e.Id, e.Name, e.MuscleGroup, e.Equipment })
            .ToListAsync(cancellationToken);

        var mastery = (await db.ExerciseMasteries.AsNoTracking()
                .Where(m => m.MemberId == memberId)
                .Select(m => new { m.ExerciseId, m.Sessions, m.BestWeightKg, m.LastTrainedAt })
                .ToListAsync(cancellationToken))
            .ToDictionary(m => m.ExerciseId);

        var passport = GymPassportPolicy.Build(catalogue.Select(e =>
        {
            var done = mastery.GetValueOrDefault(e.Id);
            return new PassportCatalogueEntry(
                e.Id,
                e.Name,
                e.MuscleGroup,
                e.Equipment,
                done?.Sessions ?? 0,
                done?.BestWeightKg ?? 0m,
                // Dated on the gym's clock, so "last trained" agrees with every other day on screen.
                done is null ? null : GymDay.Of(done.LastTrainedAt, zone));
        }), today);

        return new MyPassportDto(
            passport.Tried,
            passport.Available,
            passport.PercentCovered,
            passport.Complete,
            passport.Stamps
                .Select(s => new MyPassportStampDto(
                    s.Equipment,
                    s.Tried,
                    s.Available,
                    s.Complete,
                    s.Entries
                        .Select(Entry)
                        .ToList()))
                .ToList(),
            passport.GoneQuiet.Select(Entry).ToList());
    }

    private static MyPassportEntryDto Entry(PassportEntry e) => new(
        e.ExerciseId, e.ExerciseName, e.MuscleGroup, e.Tried, e.Sessions, e.BestWeightKg,
        e.LastTrained, e.DaysSinceLastTrained, e.GoneQuiet);
}
