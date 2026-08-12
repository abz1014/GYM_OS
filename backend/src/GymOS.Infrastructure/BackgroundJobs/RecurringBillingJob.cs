using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Billing;
using GymOS.Domain.Members;
using GymOS.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GymOS.Infrastructure.Persistence;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// The recurring-revenue engine. Two responsibilities, in order, each idempotent so a re-run is safe:
///
/// 1. RAISE — any auto-renewing membership reaching its EndDate gets a renewal invoice and a
///    RecurringBillingAttempt (the dunning record) if it doesn't already have a live one.
/// 2. COLLECT — every attempt due today is charged through IPaymentGateway. Success renews the
///    membership for another period and marks the invoice paid; a decline schedules the next retry
///    per BillingRetryPolicy and notifies the member, and once retries are exhausted the membership
///    is frozen rather than left silently unpaid.
///
/// Runs across every tenant with IgnoreQueryFilters() + explicit scoping, matching the other jobs
/// (there is no ambient user/tenant in a background job).
/// </summary>
public class RecurringBillingJob(
    GymOsDbContext db,
    IPaymentGateway paymentGateway,
    IDateTimeProvider dateTimeProvider,
    ILogger<RecurringBillingJob> logger)
{
    private const string DunningTemplateCode = "payment-failed";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var raised = await RaiseDueRenewalsAsync(today, cancellationToken);
        var collected = await CollectDueAttemptsAsync(today, cancellationToken);

        logger.LogInformation("Recurring billing raised {Raised} renewal invoice(s) and processed {Collected} charge attempt(s)", raised, collected);
        return raised + collected;
    }

    private async Task<int> RaiseDueRenewalsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var dueMemberships = await db.MemberMemberships.IgnoreQueryFilters()
            .Include(mm => mm.Member)
            .Include(mm => mm.MembershipPlan)
            .Where(mm => mm.AutoRenew
                         && mm.Status == MemberMembershipStatus.Active
                         && mm.EndDate <= today
                         // Skip any membership that already has a live dunning record.
                         && !db.RecurringBillingAttempts.IgnoreQueryFilters()
                             .Any(a => a.MemberMembershipId == mm.Id && a.Status == RecurringBillingStatus.Pending))
            .ToListAsync(cancellationToken);

        // Invoice numbers are unique per tenant, and this batch inserts several at once before a
        // single SaveChanges — so the next number is tracked per tenant in memory here. Recomputing
        // COUNT(*) per membership would hand every renewal in the same run an identical number and
        // trip the unique index (found by running this against real seeded data).
        var nextSequenceByTenant = new Dictionary<Guid, int>();

        foreach (var membership in dueMemberships)
        {
            if (membership.Member is null || membership.MembershipPlan is null)
            {
                continue;
            }

            var tenantId = membership.Member.TenantId;

            if (!nextSequenceByTenant.TryGetValue(tenantId, out var sequence))
            {
                sequence = await db.Invoices.IgnoreQueryFilters().CountAsync(i => i.TenantId == tenantId, cancellationToken) + 1;
            }

            nextSequenceByTenant[tenantId] = sequence + 1;
            var amount = membership.MembershipPlan.Price;

            var invoice = new Invoice
            {
                TenantId = tenantId,
                BranchId = membership.Member.BranchId,
                MemberId = membership.MemberId,
                InvoiceNumber = $"INV-{today.Year}-{sequence:D6}",
                IssueDate = today,
                DueDate = today.AddDays(7),
                Status = InvoiceStatus.Issued,
                Subtotal = amount,
                TaxAmount = 0,
                DiscountAmount = 0,
                TotalAmount = amount,
                Currency = membership.MembershipPlan.Currency,
                Notes = $"Auto-renewal — {membership.MembershipPlan.Name}"
            };
            invoice.Lines.Add(new InvoiceLine
            {
                ItemType = InvoiceLineItemType.MembershipFee,
                Description = $"{membership.MembershipPlan.Name} renewal",
                Quantity = 1,
                UnitPrice = amount
            });
            db.Invoices.Add(invoice);

            db.RecurringBillingAttempts.Add(new RecurringBillingAttempt
            {
                TenantId = tenantId,
                BranchId = membership.Member.BranchId,
                MemberMembershipId = membership.Id,
                MemberId = membership.MemberId,
                Invoice = invoice,
                Status = RecurringBillingStatus.Pending,
                FailedAttempts = 0,
                NextAttemptDate = today,
                Amount = amount,
                Currency = membership.MembershipPlan.Currency
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return dueMemberships.Count;
    }

    private async Task<int> CollectDueAttemptsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var dueAttempts = await db.RecurringBillingAttempts.IgnoreQueryFilters()
            .Include(a => a.Invoice)
            .Include(a => a.MemberMembership!).ThenInclude(mm => mm.MembershipPlan)
            .Where(a => a.Status == RecurringBillingStatus.Pending && a.NextAttemptDate <= today)
            .ToListAsync(cancellationToken);

        foreach (var attempt in dueAttempts)
        {
            var result = await paymentGateway.ChargeAsync(
                attempt.Amount, attempt.Currency, $"Membership renewal {attempt.Invoice?.InvoiceNumber}", cancellationToken);

            attempt.LastAttemptDate = today;

            if (result.Success)
            {
                await ApplySuccessAsync(attempt, result.TransactionId, cancellationToken);
            }
            else
            {
                await ApplyFailureAsync(attempt, result.ErrorMessage, today, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return dueAttempts.Count;
    }

    private async Task ApplySuccessAsync(RecurringBillingAttempt attempt, string? transactionId, CancellationToken cancellationToken)
    {
        attempt.Status = RecurringBillingStatus.Succeeded;
        attempt.LastFailureReason = null;

        // Payment isn't tenant-scoped itself — it inherits scope from the invoice it settles.
        db.Payments.Add(new Payment
        {
            InvoiceId = attempt.InvoiceId,
            Amount = attempt.Amount,
            Method = PaymentMethod.Card,
            Status = PaymentStatus.Completed,
            PaidAt = dateTimeProvider.UtcNow,
            GatewayTransactionId = transactionId
        });

        if (attempt.Invoice is not null)
        {
            /*
             * Derived from what is actually on the invoice, not asserted.
             *
             * This line read `Status = InvoiceStatus.Paid` unconditionally, which is only true when
             * the renewal charge is the ONLY money against that invoice. A member who part-pays a
             * renewal at the front desk on day one still has a Pending attempt for the full amount,
             * so the day-three retry charged the whole renewal again and then declared the invoice
             * settled — the card taken twice and the evidence overwritten in the same statement.
             *
             * The gateway charge above is a separate problem this does not fix: it should be for the
             * outstanding balance rather than attempt.Amount. Deriving the status at least stops the
             * invoice lying about it, and leaves a PartiallyPaid row visible instead of a Paid one
             * nobody would look at again.
             */
            var invoice = attempt.Invoice;

            var completedPayments = await db.Payments
                .Where(p => p.InvoiceId == invoice.Id && p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount, cancellationToken) + attempt.Amount;

            var completedRefunds = await db.Refunds
                .Where(r => r.Payment != null && r.Payment.InvoiceId == invoice.Id && r.Status == RefundStatus.Completed)
                .SumAsync(r => r.Amount, cancellationToken);

            invoice.Status = InvoiceStatusPolicy.Derive(
                invoice.TotalAmount, completedPayments, completedRefunds,
                invoice.DueDate, DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));
        }

        // Extend the membership by one more plan period, starting where the old one ended, so
        // renewals chain continuously rather than drifting from the original signup date.
        var membership = attempt.MemberMembership;
        if (membership?.MembershipPlan is not null)
        {
            membership.StartDate = membership.EndDate;
            membership.EndDate = membership.EndDate.AddDays(membership.MembershipPlan.DurationDays);
            membership.Status = MemberMembershipStatus.Active;
            membership.InvoiceId = attempt.InvoiceId;

            var member = await db.Members.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == membership.MemberId, cancellationToken);
            if (member is not null && member.Status is MemberStatus.Expired or MemberStatus.Frozen)
            {
                member.Status = MemberStatus.Active;
            }
        }
    }

    private async Task ApplyFailureAsync(RecurringBillingAttempt attempt, string? error, DateOnly today, CancellationToken cancellationToken)
    {
        attempt.FailedAttempts++;
        attempt.LastFailureReason = error ?? "Payment declined.";

        var nextDate = BillingRetryPolicy.NextAttemptDate(attempt.FailedAttempts, today);

        if (nextDate is not null)
        {
            attempt.NextAttemptDate = nextDate.Value;
            await ScheduleDunningNotificationAsync(attempt, cancellationToken);
            return;
        }

        // Out of retries: stop chasing and suspend access until someone settles it.
        attempt.Status = RecurringBillingStatus.Abandoned;

        if (attempt.MemberMembership is not null)
        {
            attempt.MemberMembership.Status = MemberMembershipStatus.Frozen;
        }

        if (attempt.Invoice is not null)
        {
            attempt.Invoice.Status = InvoiceStatus.Overdue;
        }

        await ScheduleDunningNotificationAsync(attempt, cancellationToken);
    }

    private async Task ScheduleDunningNotificationAsync(RecurringBillingAttempt attempt, CancellationToken cancellationToken)
    {
        var template = await db.NotificationTemplates.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == attempt.TenantId && t.Code == DunningTemplateCode, cancellationToken);

        if (template is null)
        {
            return;
        }

        db.ScheduledNotifications.Add(new ScheduledNotification
        {
            TenantId = attempt.TenantId,
            BranchId = attempt.BranchId,
            NotificationTemplateId = template.Id,
            RecipientMemberId = attempt.MemberId,
            ScheduledFor = dateTimeProvider.UtcNow,
            Status = ScheduledNotificationStatus.Pending,
            RelatedEntityType = nameof(RecurringBillingAttempt),
            RelatedEntityId = attempt.Id
        });
    }
}
