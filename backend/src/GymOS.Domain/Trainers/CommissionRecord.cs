using GymOS.Domain.Common;

namespace GymOS.Domain.Trainers;

public class CommissionRecord : BaseEntity
{
    public Guid TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    public Guid? InvoiceId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Period { get; set; }

    public CommissionStatus Status { get; set; } = CommissionStatus.Pending;
}
