using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record CreateCommissionRecordCommand(Guid TrainerId, decimal Amount, DateOnly Period) : ICommand<Guid>;

public class CreateCommissionRecordCommandValidator : AbstractValidator<CreateCommissionRecordCommand>
{
    public CreateCommissionRecordCommandValidator()
    {
        RuleFor(x => x.TrainerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class CreateCommissionRecordCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCommissionRecordCommand, Guid>
{
    public async Task<Guid> Handle(CreateCommissionRecordCommand request, CancellationToken cancellationToken)
    {
        var trainerExists = await db.Trainers.AnyAsync(t => t.Id == request.TrainerId, cancellationToken);
        if (!trainerExists)
        {
            throw new NotFoundException(nameof(Trainer), request.TrainerId);
        }

        var record = new CommissionRecord
        {
            TrainerId = request.TrainerId,
            Amount = request.Amount,
            Period = request.Period,
            Status = CommissionStatus.Pending
        };

        db.CommissionRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return record.Id;
    }
}
