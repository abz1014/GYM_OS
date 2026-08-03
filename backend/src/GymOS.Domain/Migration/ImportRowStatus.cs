namespace GymOS.Domain.Migration;

public enum ImportRowStatus
{
    Pending,
    Valid,
    Invalid,
    Committed,
    Skipped
}
