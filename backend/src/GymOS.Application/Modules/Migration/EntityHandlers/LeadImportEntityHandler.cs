using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Crm.Commands;
using GymOS.Domain.Crm;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

public class LeadImportEntityHandler(IApplicationDbContext db) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Lead;

    public IReadOnlyList<string> RequiredFields { get; } = ["FirstName", "LastName", "Email", "Source"];

    public IReadOnlyList<string> OptionalFields { get; } = ["Phone"];

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

        if (!Enum.TryParse<LeadSource>(fields["Source"], ignoreCase: true, out _))
        {
            return ImportValidationResult.Invalid(
                $"'{fields["Source"]}' is not a valid source (expected WalkIn, Referral, SocialMedia, Website, Advertisement, or Other).");
        }

        var alreadyExists = await db.Leads.AnyAsync(l => l.Email == email, cancellationToken);
        if (alreadyExists)
        {
            return ImportValidationResult.Duplicate($"A lead with email '{email}' already exists.");
        }

        return ImportValidationResult.Ok();
    }

    public Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        var source = Enum.Parse<LeadSource>(fields["Source"], ignoreCase: true);

        return sender.Send(
            new CreateLeadCommand(fields["FirstName"], fields["LastName"], fields["Email"], fields.GetValueOrDefault("Phone"), source, branchId, null),
            cancellationToken);
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(Lead), mappedEntityId);

        // Lead has neither a soft-delete flag nor an IsActive switch — Lost is its own domain's
        // terminal status, the same role Retired plays for Asset and IsActive=false plays for Trainer.
        lead.Stage = LeadStage.Lost;

        await db.SaveChangesAsync(cancellationToken);
    }
}
