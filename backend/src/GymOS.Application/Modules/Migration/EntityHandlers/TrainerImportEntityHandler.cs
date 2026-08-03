using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Trainers.Commands;
using GymOS.Domain.Migration;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

public class TrainerImportEntityHandler(IApplicationDbContext db) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Trainer;

    public IReadOnlyList<string> RequiredFields { get; } = ["FirstName", "LastName", "Email", "Specialties", "CommissionRate"];

    public IReadOnlyList<string> OptionalFields { get; } = ["Bio"];

    public string? GetNaturalKey(IReadOnlyDictionary<string, string> fields)
        => fields.TryGetValue("Email", out var email) && !string.IsNullOrWhiteSpace(email) ? email.Trim().ToLowerInvariant() : null;

    public async Task<ImportValidationResult> ValidateAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        foreach (var required in RequiredFields)
        {
            if (!fields.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return ImportValidationResult.Invalid($"Missing required field '{required}'.");
            }
        }

        var email = fields["Email"];
        if (!email.Contains('@'))
        {
            return ImportValidationResult.Invalid($"'{email}' is not a valid email address.");
        }

        if (!decimal.TryParse(fields["CommissionRate"], out var rate) || rate < 0 || rate > 100)
        {
            return ImportValidationResult.Invalid($"'{fields["CommissionRate"]}' is not a valid commission rate (0-100).");
        }

        var alreadyExists = await db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (alreadyExists)
        {
            return ImportValidationResult.Duplicate($"A user with email '{email}' already exists.");
        }

        return ImportValidationResult.Ok();
    }

    public async Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        var rate = decimal.Parse(fields["CommissionRate"]);

        var result = await sender.Send(
            new CreateTrainerCommand(fields["FirstName"], fields["LastName"], fields["Email"], branchId, fields["Specialties"], rate, fields.GetValueOrDefault("Bio")),
            cancellationToken);

        return result.TrainerId;
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trainer), mappedEntityId);

        trainer.IsActive = false;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == trainer.UserId, cancellationToken);
        if (user is not null)
        {
            user.IsActive = false;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
