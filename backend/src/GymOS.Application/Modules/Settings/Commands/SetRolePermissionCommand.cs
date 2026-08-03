using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

public record SetRolePermissionCommand(Guid RoleId, Guid PermissionId, bool Granted) : ICommand<Unit>;

public class SetRolePermissionCommandValidator : AbstractValidator<SetRolePermissionCommand>
{
    public SetRolePermissionCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PermissionId).NotEmpty();
    }
}

public class SetRolePermissionCommandHandler(IApplicationDbContext db) : IRequestHandler<SetRolePermissionCommand, Unit>
{
    public async Task<Unit> Handle(SetRolePermissionCommand request, CancellationToken cancellationToken)
    {
        var roleExists = await db.Roles.AnyAsync(r => r.Id == request.RoleId, cancellationToken);
        if (!roleExists)
        {
            throw new NotFoundException(nameof(Role), request.RoleId);
        }

        var existing = await db.RolePermissions.FirstOrDefaultAsync(
            rp => rp.RoleId == request.RoleId && rp.PermissionId == request.PermissionId, cancellationToken);

        if (request.Granted && existing is null)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = request.RoleId, PermissionId = request.PermissionId });
        }
        else if (!request.Granted && existing is not null)
        {
            db.RolePermissions.Remove(existing);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
