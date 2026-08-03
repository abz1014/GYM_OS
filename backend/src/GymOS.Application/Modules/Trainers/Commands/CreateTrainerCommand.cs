using FluentValidation;
using GymOS.Application.Common;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Trainers.Dtos;
using GymOS.Domain.Identity;
using GymOS.Domain.Trainers;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record CreateTrainerCommand(
    string FirstName, string LastName, string Email, Guid BranchId, string Specialties, decimal CommissionRate, string? Bio)
    : ICommand<CreateTrainerResultDto>;

public class CreateTrainerCommandValidator : AbstractValidator<CreateTrainerCommand>
{
    public CreateTrainerCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CommissionRate).InclusiveBetween(0, 100);
    }
}

public class CreateTrainerCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IPasswordHasher passwordHasher)
    : IRequestHandler<CreateTrainerCommand, CreateTrainerResultDto>
{
    public async Task<CreateTrainerResultDto> Handle(CreateTrainerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var trainerRole = await db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == RoleNames.Trainer, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Identity.Role), RoleNames.Trainer);

        var emailTaken = await db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailTaken)
        {
            throw new ValidationException($"Email \"{request.Email}\" is already in use.");
        }

        var temporaryPassword = TokenHasher.GenerateTemporaryPassword();

        var user = new User
        {
            TenantId = tenantId,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(temporaryPassword),
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true
        };

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = trainerRole.Id });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = request.BranchId });

        var trainer = new Trainer
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            UserId = user.Id,
            Specialties = request.Specialties,
            CommissionRate = request.CommissionRate,
            Bio = request.Bio,
            IsActive = true
        };

        db.Trainers.Add(trainer);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateTrainerResultDto(trainer.Id, temporaryPassword);
    }
}
