namespace GymOS.Domain.Migration;

public enum ImportStatus
{
    Uploaded,
    Parsing,
    Validated,
    Committing,
    Completed,
    Failed,
    RolledBack
}
