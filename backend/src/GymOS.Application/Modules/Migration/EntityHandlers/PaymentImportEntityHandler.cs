using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Billing.Commands;
using GymOS.Domain.Billing;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

/// <summary>
/// Imports a historical payment against an already-existing member (resolved by email). Creates a
/// minimal paid invoice-of-record via the shared CreateInvoiceCommand — safe to reuse here, since
/// it has no gateway/notification side effects for an Other-type line — then records the payment
/// itself via ImportPaymentCommand rather than the live RecordPaymentCommand; see that command's
/// doc comment for why.
/// </summary>
public class PaymentImportEntityHandler(IApplicationDbContext db) : IImportEntityHandler
{
    public ImportEntityType EntityType => ImportEntityType.Payment;

    public IReadOnlyList<string> RequiredFields { get; } = ["MemberEmail", "Amount", "PaidAt", "Method"];

    public IReadOnlyList<string> OptionalFields { get; } = ["Notes", "Currency"];

    public string? GetNaturalKey(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("MemberEmail", out var email) || string.IsNullOrWhiteSpace(email)
            || !fields.TryGetValue("PaidAt", out var paidAt) || string.IsNullOrWhiteSpace(paidAt)
            || !fields.TryGetValue("Amount", out var amount) || string.IsNullOrWhiteSpace(amount))
        {
            return null;
        }

        return $"{email.Trim().ToLowerInvariant()}|{paidAt.Trim()}|{amount.Trim()}";
    }

    public async Task<ImportValidationResult> ValidateAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        foreach (var required in RequiredFields)
        {
            if (!fields.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return ImportValidationResult.Invalid($"Missing required field '{required}'.");
            }
        }

        var email = fields["MemberEmail"];
        var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == email, cancellationToken);
        if (member is null)
        {
            return ImportValidationResult.Invalid($"No member found with email '{email}'. Import members first.");
        }

        if (!decimal.TryParse(fields["Amount"], out var amount) || amount <= 0)
        {
            return ImportValidationResult.Invalid($"'{fields["Amount"]}' is not a valid positive amount.");
        }

        if (!ImportDateTimeParsing.TryParseUtc(fields["PaidAt"], out _))
        {
            return ImportValidationResult.Invalid($"'{fields["PaidAt"]}' is not a valid date/time for PaidAt.");
        }

        if (!Enum.TryParse<PaymentMethod>(fields["Method"], ignoreCase: true, out _))
        {
            return ImportValidationResult.Invalid($"'{fields["Method"]}' is not a valid payment method (Cash, Card, BankTransfer, Other).");
        }

        return ImportValidationResult.Ok();
    }

    public async Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().FirstAsync(m => m.Email == fields["MemberEmail"], cancellationToken);
        var amount = decimal.Parse(fields["Amount"]);
        ImportDateTimeParsing.TryParseUtc(fields["PaidAt"], out var paidAt);
        var method = Enum.Parse<PaymentMethod>(fields["Method"], ignoreCase: true);
        var paidDate = DateOnly.FromDateTime(paidAt.UtcDateTime);

        var currency = fields.TryGetValue("Currency", out var curRaw) && !string.IsNullOrWhiteSpace(curRaw)
            ? curRaw.Trim().ToUpperInvariant()
            : "USD";

        var notes = fields.TryGetValue("Notes", out var n) && !string.IsNullOrWhiteSpace(n) ? n : "Imported payment";

        var invoiceId = await sender.Send(
            new CreateInvoiceCommand(
                member.Id, branchId, paidDate, paidDate, 0m, 0m, currency, notes,
                [new CreateInvoiceLineInput(InvoiceLineItemType.Other, notes, 1, amount)]),
            cancellationToken);

        return await sender.Send(new ImportPaymentCommand(invoiceId, method, amount, paidAt), cancellationToken);
    }

    public async Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == mappedEntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), mappedEntityId);

        // The invoice was purely a byproduct of this import — a synthetic invoice-of-record never
        // referenced elsewhere — so deleting it cascades the payment and its line (see
        // BillingConfigurations: Invoice.Payments/Lines are both DeleteBehavior.Cascade), leaving
        // nothing orphaned. PaymentStatus has no Cancelled/Voided value, so there's no soft-state
        // to fall back to here either way.
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == payment.InvoiceId, cancellationToken);
        if (invoice is not null)
        {
            db.Invoices.Remove(invoice);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
