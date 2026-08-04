using GymOS.Domain.Crm;
using Shouldly;

namespace GymOS.Domain.Tests.Crm;

/// <summary>
/// The lead-triage score: stage progress, source quality, engagement, and recency each contribute,
/// and the total is clamped to a 0-100 band so a lead near either extreme doesn't read as an
/// out-of-scale outlier on the CRM board.
/// </summary>
public class LeadScorePolicyTests
{
    [Fact]
    public void A_brand_new_untouched_lead_from_a_weak_source_scores_low()
    {
        // Stage(Lead)=10 + Source(Other)=0 + Engagement(0)=0 + Recency(null)=-5 -> 5.
        LeadScorePolicy.CalculateScore(LeadStage.Lead, LeadSource.Other, activityCount: 0, daysSinceLastActivity: null)
            .ShouldBe(5);
    }

    [Fact]
    public void A_trial_lead_from_a_referral_recently_engaged_scores_high()
    {
        // Stage(Trial)=55 + Source(Referral)=15 + Engagement(3*5=15) + Recency(<=3)=15 -> 100 (clamped from 100, no clamp needed but exercises the top end).
        LeadScorePolicy.CalculateScore(LeadStage.Trial, LeadSource.Referral, activityCount: 3, daysSinceLastActivity: 2)
            .ShouldBe(100);
    }

    [Fact]
    public void Score_never_exceeds_the_maximum_even_when_every_factor_is_maxed()
    {
        // Stage(Trial)=55 + Source(Referral)=15 + Engagement(capped 20) + Recency(<=3)=15 = 105 -> clamped to 100.
        LeadScorePolicy.CalculateScore(LeadStage.Trial, LeadSource.Referral, activityCount: 10, daysSinceLastActivity: 1)
            .ShouldBe(LeadScorePolicy.MaxScore);
    }

    [Fact]
    public void Score_never_drops_below_the_minimum()
    {
        // Stage(Lead)=10 + Source(Other)=0 + Engagement(0) + Recency(>14)=-15 = -5 -> clamped to 0.
        LeadScorePolicy.CalculateScore(LeadStage.Lead, LeadSource.Other, activityCount: 0, daysSinceLastActivity: 30)
            .ShouldBe(LeadScorePolicy.MinScore);
    }

    [Fact]
    public void A_lead_gone_cold_scores_lower_than_one_never_contacted()
    {
        var neverContacted = LeadScorePolicy.CalculateScore(LeadStage.FollowUp, LeadSource.Website, 0, null);
        var wentCold = LeadScorePolicy.CalculateScore(LeadStage.FollowUp, LeadSource.Website, 1, 20);

        wentCold.ShouldBeLessThan(neverContacted);
    }

    [Fact]
    public void Engagement_bonus_is_capped_so_activity_spam_cannot_outrank_pipeline_progress()
    {
        var manyActivitiesEarlyStage = LeadScorePolicy.CalculateScore(LeadStage.Lead, LeadSource.Other, activityCount: 20, daysSinceLastActivity: 1);
        var fewActivitiesLateStage = LeadScorePolicy.CalculateScore(LeadStage.Trial, LeadSource.Other, activityCount: 1, daysSinceLastActivity: 1);

        fewActivitiesLateStage.ShouldBeGreaterThan(manyActivitiesEarlyStage);
    }
}
