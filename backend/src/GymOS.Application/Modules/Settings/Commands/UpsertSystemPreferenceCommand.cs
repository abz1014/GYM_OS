using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

public record UpsertSystemPreferenceCommand(Guid? BranchId, string Key, string Value, string? Description) : ICommand<Guid>;

public class UpsertSystemPreferenceCommandValidator : AbstractValidator<UpsertSystemPreferenceCommand>
{
    public UpsertSystemPreferenceCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).NotEmpty();
    }
}

public class UpsertSystemPreferenceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpsertSystemPreferenceCommand, Guid>
{
    public async Task<Guid> Handle(UpsertSystemPreferenceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var existing = await db.SystemPreferences.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.BranchId == request.BranchId && p.Key == request.Key, cancellationToken);

        if (existing is not null)
        {
            existing.Value = request.Value;
            existing.Description = request.Description;
            await db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var preference = new SystemPreference
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            Key = request.Key,
            Value = request.Value,
            Description = request.Description
        };

        db.SystemPreferences.Add(preference);
        await db.SaveChangesAsync(cancellationToken);

        return preference.Id;
    }
}
