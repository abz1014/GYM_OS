using GymOS.Domain.Crm;
using Shouldly;

namespace GymOS.Domain.Tests.Crm;

/// <summary>
/// The automated nurture escalation: only fires for leads nobody has touched, only in the two
/// early pipeline stages, and only ever sends the single most-advanced step a lead qualifies for
/// — never a backlog of every skipped marker at once.
/// </summary>
public class LeadDripPolicyTests
{
    [Fact]
    public void No_drip_before_day_3()
    {
        LeadDripPolicy.GetDueDripDay(LeadStage.Lead, hasAnyActivity: false, daysSinceCreated: 2, alreadySentDays: [])
            .ShouldBeNull();
    }

    [Fact]
    public void Day_3_step_fires_once_the_lead_is_3_days_old_and_untouched()
    {
        LeadDripPolicy.GetDueDripDay(LeadStage.Lead, hasAnyActivity: false, daysSinceCreated: 3, alreadySentDays: [])
            .ShouldBe(3);
    }

    [Fact]
    public void A_lead_with_any_logged_activity_never_gets_a_drip_message()
    {
        LeadDripPolicy.GetDueDripDay(LeadStage.Lead, hasAnyActivity: true, daysSinceCreated: 30, alreadySentDays: [])
            .ShouldBeNull();
    }

    [Theory]
    [InlineData(LeadStage.Trial)]
    [InlineData(LeadStage.Member)]
    [InlineData(LeadStage.Lost)]
    public void Only_Lead_and_FollowUp_stages_are_ever_eligible(LeadStage stage)
    {
        LeadDripPolicy.GetDueDripDay(stage, hasAnyActivity: false, daysSinceCreated: 30, alreadySentDays: [])
            .ShouldBeNull();
    }

    [Fact]
    public void An_already_sent_step_does_not_fire_again()
    {
        LeadDripPolicy.GetDueDripDay(LeadStage.FollowUp, hasAnyActivity: false, daysSinceCreated: 3, alreadySentDays: [3])
            .ShouldBeNull();
    }

    [Fact]
    public void A_lead_that_aged_past_multiple_unsent_markers_gets_only_the_most_advanced_one()
    {
        // 10 days old, nothing ever sent: day-3 AND day-7 both qualify — expect day-7, not day-3.
        LeadDripPolicy.GetDueDripDay(LeadStage.Lead, hasAnyActivity: false, daysSinceCreated: 10, alreadySentDays: [])
            .ShouldBe(7);
    }

    [Fact]
    public void After_day_3_already_sent_the_next_due_step_is_day_7()
    {
        LeadDripPolicy.GetDueDripDay(LeadStage.FollowUp, hasAnyActivity: false, daysSinceCreated: 8, alreadySentDays: [3])
            .ShouldBe(7);
    }

    [Fact]
    public void Once_every_step_has_been_sent_no_further_drip_fires()
    {
        LeadDripPolicy.GetDueDripDay(LeadStage.Lead, hasAnyActivity: false, daysSinceCreated: 60, alreadySentDays: [3, 7, 14])
            .ShouldBeNull();
    }
}
