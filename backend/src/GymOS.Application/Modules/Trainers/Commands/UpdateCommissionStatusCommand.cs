using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record UpdateCommissionStatusCommand(Guid CommissionRecordId, CommissionStatus Status) : ICommand<Unit>;

public class UpdateCommissionStatusCommandValidator : AbstractValidator<UpdateCommissionStatusCommand>
{
    public UpdateCommissionStatusCommandValidator() => RuleFor(x => x.CommissionRecordId).NotEmpty();
}

public class UpdateCommissionStatusCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCommissionStatusCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCommissionStatusCommand request, CancellationToken cancellationToken)
    {
        var record = await db.CommissionRecords.FirstOrDefaultAsync(c => c.Id == request.CommissionRecordId, cancellationToken)
            ?? throw new NotFoundException(nameof(CommissionRecord), request.CommissionRecordId);

        record.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
