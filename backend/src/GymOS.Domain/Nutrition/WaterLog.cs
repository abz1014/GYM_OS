using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

public class WaterLog : BaseEntity
{
    public Guid MemberId { get; set; }

    public int AmountMl { get; set; }

    public DateTimeOffset LoggedAt { get; set; }
}
