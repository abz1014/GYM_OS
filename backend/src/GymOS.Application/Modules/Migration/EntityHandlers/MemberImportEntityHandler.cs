using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Domain.Members;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

public class MemberImportEntityHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Member;

    public IReadOnlyList<string> RequiredFields { get; } = ["FirstName", "LastName", "Email"];

    public IReadOnlyList<string> OptionalFields { get; } = ["Phone", "DateOfBirth", "Gender", "Address"];

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

        if (fields.TryGetValue("DateOfBirth", out var dob) && !string.IsNullOrWhiteSpace(dob) && !DateOnly.TryParse(dob, out _))
        {
            return ImportValidationResult.Invalid($"'{dob}' is not a valid date for DateOfBirth (expected yyyy-MM-dd).");
        }

        var alreadyExists = await db.Members.AnyAsync(m => m.Email == email, cancellationToken);
        if (alreadyExists)
        {
            return ImportValidationResult.Duplicate($"A member with email '{email}' already exists.");
        }

        return ImportValidationResult.Ok();
    }

    public Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        DateOnly? dateOfBirth = fields.TryGetValue("DateOfBirth", out var dob) && DateOnly.TryParse(dob, out var parsed) ? parsed : null;

        return sender.Send(
            new CreateMemberCommand(
                fields["FirstName"],
                fields["LastName"],
                fields["Email"],
                fields.GetValueOrDefault("Phone"),
                dateOfBirth,
                fields.GetValueOrDefault("Gender"),
                fields.GetValueOrDefault("Address"),
                branchId),
            cancellationToken);
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(Member), mappedEntityId);

        member.IsDeleted = true;
        member.DeletedAt = dateTimeProvider.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
