using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

public record UpdateBranchCommand(
    Guid Id, string Name, string AddressLine, string City, string Country, string TimeZone, string Currency, bool IsActive,
    int? Capacity = null)
    : ICommand<Unit>;

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.Country).NotEmpty();
        // Null means "not told"; a supplied figure has to be a room somebody could stand in.
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(Branch.MaxCapacity).When(x => x.Capacity.HasValue);
    }
}

public class UpdateBranchCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateBranchCommand, Unit>
{
    public async Task<Unit> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        branch.Name = request.Name;
        branch.AddressLine = request.AddressLine;
        branch.City = request.City;
        branch.Country = request.Country;
        branch.TimeZone = request.TimeZone;
        branch.Currency = request.Currency;
        branch.IsActive = request.IsActive;
        // Clearing it back to null is legitimate: a gym that set a figure it no longer trusts should be
        // able to withdraw it, and every consumer already falls back to the bare count.
        branch.Capacity = request.Capacity;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
