namespace GymOS.Domain.Experience;

/// <summary>
/// How recovered a member (or one of their muscle groups) is, ordered from most-rested to
/// most-taxed. Purely derived from logged training load and rest — no wearable data (the blueprint's
/// wearable sync stays a deferred <c>IWearableSyncProvider</c>).
/// </summary>
public enum RecoveryStatus
{
    /// <summary>Well-rested — hasn't trained recently. Encourage a session.</summary>
    Fresh,

    /// <summary>Balanced load with adequate rest — good to train.</summary>
    Ready,

    /// <summary>High recent load with limited rest — ease off or add a recovery day.</summary>
    Fatigued,

    /// <summary>Very high frequency with no logged rest — should take a recovery day.</summary>
    OvertrainingRisk
}
