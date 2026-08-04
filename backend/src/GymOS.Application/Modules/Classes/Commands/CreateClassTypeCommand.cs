using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Classes;
using MediatR;

namespace GymOS.Application.Modules.Classes.Commands;

public record CreateClassTypeCommand(
    string Name, string? Description, int DefaultDurationMinutes, int DefaultCapacity, string? ColorHex) : ICommand<Guid>;

public class CreateClassTypeCommandValidator : AbstractValidator<CreateClassTypeCommand>
{
    public CreateClassTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(5, 480);
        RuleFor(x => x.DefaultCapacity).InclusiveBetween(1, 1000);
    }
}

public class CreateClassTypeCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateClassTypeCommand, Guid>
{
    public async Task<Guid> Handle(CreateClassTypeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var classType = new ClassType
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            DefaultDurationMinutes = request.DefaultDurationMinutes,
            DefaultCapacity = request.DefaultCapacity,
            ColorHex = request.ColorHex,
            IsActive = true
        };

        db.ClassTypes.Add(classType);
        await db.SaveChangesAsync(cancellationToken);

        return classType.Id;
    }
}
