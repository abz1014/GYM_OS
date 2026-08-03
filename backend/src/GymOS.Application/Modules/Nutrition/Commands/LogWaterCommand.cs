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
    public LogWaterCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.AmountMl).GreaterThan(0);
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
