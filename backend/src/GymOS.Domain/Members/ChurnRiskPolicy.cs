namespace GymOS.Domain.Members;

/// <summary>
/// Decides which paying members have quietly stopped turning up — the input to the automated
/// win-back message. Pure, because "how long is too long, and who do we leave alone" is the rule
/// an owner will want to tune, and it should be provable without a database.
///
/// Deliberately conservative: only members who are still paying (Active) and who HAVE come before
/// are chased. A brand-new member who hasn't visited yet is an onboarding problem, not a churn
/// one, and messaging them "we miss you" would read as broken.
/// </summary>
public static class ChurnRiskPolicy
{
    /// <summary>Days without a check-in before a member is considered at risk.</summary>
    public const int InactivityThresholdDays = 14;

    /// <summary>Don't re-nag: wait this long after a win-back before sending another.</summary>
    public const int ResendCooldownDays = 30;

    /// <summary>
    /// Whether to send a win-back now.
    /// <paramref name="lastCheckInDate"/> is null when the member has never visited.
    /// <paramref name="lastWinBackSentDate"/> is null when they've never been chased.
    /// </summary>
    public static bool ShouldSendWinBack(
        MemberStatus status, DateOnly? lastCheckInDate, DateOnly? lastWinBackSentDate, DateOnly today)
    {
        // Only chase members who are still paying — expired/cancelled members are a different
        // (reactivation) conversation, and frozen members asked to pause on purpose.
        if (status != MemberStatus.Active)
        {
            return false;
        }

        // Never visited → onboarding, not churn.
        if (lastCheckInDate is null)
        {
            return false;
        }

        if (today.DayNumber - lastCheckInDate.Value.DayNumber < InactivityThresholdDays)
        {
            return false;
        }

        // Already chased recently → stay quiet.
        return lastWinBackSentDate is null
               || today.DayNumber - lastWinBackSentDate.Value.DayNumber >= ResendCooldownDays;
    }
}
