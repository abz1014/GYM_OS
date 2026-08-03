using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Equipment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Equipment.Commands;

public record UpdateAssetStatusCommand(Guid AssetId, AssetStatus Status) : ICommand<Unit>;

public class UpdateAssetStatusCommandValidator : AbstractValidator<UpdateAssetStatusCommand>
{
    public UpdateAssetStatusCommandValidator() => RuleFor(x => x.AssetId).NotEmpty();
}

public class UpdateAssetStatusCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateAssetStatusCommand, Unit>
{
    public async Task<Unit> Handle(UpdateAssetStatusCommand request, CancellationToken cancellationToken)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), request.AssetId);

        asset.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
