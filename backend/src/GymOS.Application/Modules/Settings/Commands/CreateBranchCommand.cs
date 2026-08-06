using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Tenancy;
using MediatR;

namespace GymOS.Application.Modules.Settings.Commands;

public record CreateBranchCommand(
    string Name, string AddressLine, string City, string Country, string TimeZone, string Currency, int? Capacity = null)
    : ICommand<Guid>;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.Country).NotEmpty();
        // Null means "not told"; a supplied figure has to be a room somebody could stand in.
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(Branch.MaxCapacity).When(x => x.Capacity.HasValue);
    }
}

public class CreateBranchCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateBranchCommand, Guid>
{
    public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var branch = new Branch
        {
            TenantId = tenantId,
            Name = request.Name,
            AddressLine = request.AddressLine,
            City = request.City,
            Country = request.Country,
            TimeZone = request.TimeZone,
            Currency = request.Currency,
            Capacity = request.Capacity,
            IsActive = true
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }
}
