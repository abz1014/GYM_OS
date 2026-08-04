namespace GymOS.Domain.Crm;

/// <summary>
/// A 0-100 "how hot is this lead" score so staff can triage a long pipeline list without reading
/// every row — how far along the pipeline they are, how good the source historically converts,
/// how much staff has already engaged, and how recently. Pure (facts in, number out) so the
/// weighting is unit-tested and can be re-tuned without touching the query that calls it.
/// </summary>
public static class LeadScorePolicy
{
    public const int MinScore = 0;
    public const int MaxScore = 100;

    public static int CalculateScore(LeadStage stage, LeadSource source, int activityCount, int? daysSinceLastActivity)
    {
        var score = StageWeight(stage) + SourceWeight(source) + EngagementWeight(activityCount) + RecencyWeight(daysSinceLastActivity);
        return Math.Clamp(score, MinScore, MaxScore);
    }

    // Further along the pipeline = closer to converting = hotter, regardless of how they got here.
    private static int StageWeight(LeadStage stage) => stage switch
    {
        LeadStage.Lead => 10,
        LeadStage.FollowUp => 30,
        LeadStage.Trial => 55,
        LeadStage.Member => 100,
        LeadStage.Lost => 0,
        _ => 0
    };

    // A referral from an existing member is warmer than a cold ad click before a single call happens.
    private static int SourceWeight(LeadSource source) => source switch
    {
        LeadSource.Referral => 15,
        LeadSource.Website => 10,
        LeadSource.WalkIn => 8,
        LeadSource.SocialMedia => 5,
        LeadSource.Advertisement => 3,
        LeadSource.Other => 0,
        _ => 0
    };

    // Capped so a lead staff has hammered with activities doesn't outrank one that's simply further
    // along — engagement matters, but it's a modifier, not the main signal.
    private static int EngagementWeight(int activityCount) => Math.Min(activityCount * 5, 20);

    // No activity at all is worse than a stale one: at least a stale contact proves the lead engaged
    // once. A lead going quiet for two-plus weeks is going cold and should sort down, not up.
    private static int RecencyWeight(int? daysSinceLastActivity)
    {
        if (daysSinceLastActivity is null)
        {
            return -5;
        }

        return daysSinceLastActivity switch
        {
            <= 3 => 15,
            <= 7 => 5,
            <= 14 => 0,
            _ => -15
        };
    }
}
