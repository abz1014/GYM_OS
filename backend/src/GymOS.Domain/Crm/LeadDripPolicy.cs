namespace GymOS.Domain.Crm;

/// <summary>
/// Decides when a fully cold lead (nobody has logged a single activity against it) should get an
/// automated nurture message, so a lead that falls through the cracks isn't lost purely because a
/// busy front desk never got to it. Only fires for leads still early in the pipeline (Lead/
/// FollowUp) — once staff has logged even one activity, a human is already driving and the
/// automation steps back; Trial/Member/Lost leads are being handled through a different motion
/// entirely. Pure (facts in, day-marker out) so the escalation ladder is unit-tested.
/// </summary>
public static class LeadDripPolicy
{
    /// <summary>Day markers (since the lead was created) at which an escalating nurture message fires.</summary>
    public static readonly IReadOnlyList<int> DripDays = [3, 7, 14];

    /// <summary>
    /// Which day-marker's message is due, if any. When a lead has aged past more than one unsent
    /// marker (e.g. imported already 10 days old with nothing sent), returns the most advanced one
    /// rather than firing every skipped step at once — the point is a single graduated nudge, not a
    /// backlog dump the moment the job catches up.
    /// </summary>
    public static int? GetDueDripDay(
        LeadStage stage, bool hasAnyActivity, int daysSinceCreated, IReadOnlyCollection<int> alreadySentDays)
    {
        if (stage != LeadStage.Lead && stage != LeadStage.FollowUp)
        {
            return null;
        }

        if (hasAnyActivity)
        {
            return null;
        }

        int? due = null;
        foreach (var day in DripDays)
        {
            if (daysSinceCreated >= day && !alreadySentDays.Contains(day))
            {
                due = day;
            }
        }

        return due;
    }
}
