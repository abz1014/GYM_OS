using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Billing;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job (registered daily via Hangfire in Program.cs). Closes the Billing workflow's
/// missing "Overdue" step — InvoiceStatus.Overdue was only ever set by demo seed data; nothing
/// actually flipped an unpaid invoice past its due date to Overdue.
/// </summary>
public class InvoiceOverdueTransitionJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<InvoiceOverdueTransitionJob> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var overdue = await db.Invoices.IgnoreQueryFilters()
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < today)
            .ToListAsync(cancellationToken);

        foreach (var invoice in overdue)
        {
            invoice.Status = InvoiceStatus.Overdue;
        }

        var updated = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Invoice overdue transition flipped {Count} invoice(s) to Overdue", overdue.Count);
        return updated;
    }
}
