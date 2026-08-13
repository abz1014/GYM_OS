using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Nutrition;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Commands;

public record LogWaterCommand(Guid MemberId, int AmountMl) : ICommand<Guid>;

public class LogWaterCommandValidator : AbstractValidator<LogWaterCommand>
{
    /// <summary>
    /// The most water one entry can plausibly record. A five-litre drink is already generous; this is
    /// a sanity bound on a typo, not a hydration opinion.
    /// </summary>
    public const int MaxAmountMl = 5_000;

    public LogWaterCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();

        /*
         * The upper bound lives HERE, on the shared write path, and that is the whole point.
         *
         * It used to exist only on LogMyWaterCommand — the member's own portal — while this command,
         * which the portal DELEGATES INTO and which staff call directly, accepted anything above
         * zero. The strict rule guarded the weaker caller and left the stronger one open.
         *
         * The cost was not a silly number in a list. GetNutritionReportQuery sums AmountMl, so a
         * single int.MaxValue row overflowed the sum and returned 500 for EVERY role, tenant-wide,
         * until somebody deleted the row by hand. One bad staff entry took the whole Nutrition
         * report down.
         */
        RuleFor(x => x.AmountMl).GreaterThan(0).LessThanOrEqualTo(MaxAmountMl);
    }
}

public class LogWaterCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider) : IRequestHandler<LogWaterCommand, Guid>
{
    public async Task<Guid> Handle(LogWaterCommand request, CancellationToken cancellationToken)
    {
        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        var log = new WaterLog
        {
            MemberId = request.MemberId,
            AmountMl = request.AmountMl,
            LoggedAt = dateTimeProvider.UtcNow
        };

        db.WaterLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        return log.Id;
    }
}
