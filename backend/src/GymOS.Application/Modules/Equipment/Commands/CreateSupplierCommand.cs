using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Equipment;
using MediatR;

namespace GymOS.Application.Modules.Equipment.Commands;

public record CreateSupplierCommand(string Name, string? ContactName, string? Phone, string? Email, string? Address) : ICommand<Guid>;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public class CreateSupplierCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateSupplierCommand, Guid>
{
    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var supplier = new Supplier
        {
            TenantId = tenantId,
            Name = request.Name,
            ContactName = request.ContactName,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}
