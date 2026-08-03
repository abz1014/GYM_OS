using GymOS.Domain.Common;

namespace GymOS.Domain.Billing;

public class PaymentReminder : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
