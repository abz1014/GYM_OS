using GymOS.Domain.Common;

namespace GymOS.Domain.Members;

public class MemberMeasurement : BaseEntity
{
    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public DateOnly MeasuredOn { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? BodyFatPercentage { get; set; }

    public decimal? ChestCm { get; set; }

    public decimal? WaistCm { get; set; }

    public decimal? HipCm { get; set; }

    public decimal? ArmCm { get; set; }

    public decimal? ThighCm { get; set; }

    public string? Notes { get; set; }
}
