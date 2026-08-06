using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The recommendation engine is pure, so each nudge's trigger condition is pinned here: volume trend
/// only fires on a meaningful swing, recovery advice only fires when action is warranted, and every
/// recommendation carries a non-empty explanation ("always explain").
///
/// What is no longer here is the point of the Step 9 review. A per-exercise overload alert and a
/// weakest-muscle-group focus used to live in this engine, and both were removed for restating
/// something the member already had on screen. TrainingInsightPolicy owns those two facts now, and
/// its own tests pin them.
/// </summary>
public class RecommendationPolicyTests
{
    [Theory]
    [InlineData(1000, 2000, true)]  // dropped to 50% -> notable decline
    [InlineData(3000, 1000, true)]  // jumped to 300% -> notable increase
    [InlineData(1000, 1000, false)] // flat -> nothing to say
    [InlineData(1200, 1000, false)] // 1.2x -> within the unremarkable band
    public void VolumeTrend_only_fires_on_a_meaningful_swing(decimal current, decimal previous, bool expectRecommendation)
        => (RecommendationPolicy.VolumeTrend(current, previous) is not null).ShouldBe(expectRecommendation);

    [Fact]
    public void VolumeTrend_is_null_with_no_prior_week_baseline()
        => RecommendationPolicy.VolumeTrend(500m, 0m).ShouldBeNull();

    [Theory]
    [InlineData(RecoveryStatus.Fresh, false)]
    [InlineData(RecoveryStatus.Ready, false)]
    [InlineData(RecoveryStatus.Fatigued, true)]
    [InlineData(RecoveryStatus.OvertrainingRisk, true)]
    public void RecoveryAdvice_only_fires_when_action_is_warranted(RecoveryStatus status, bool expectRecommendation)
        => (RecommendationPolicy.RecoveryAdvice(status, "some reason") is not null).ShouldBe(expectRecommendation);

    [Fact]
    public void Every_recommendation_carries_a_non_empty_explanation()
    {
        var volume = RecommendationPolicy.VolumeTrend(1000, 2000)!;
        var recovery = RecommendationPolicy.RecoveryAdvice(RecoveryStatus.Fatigued, "trained hard all week")!;
        var substitution = RecommendationPolicy.ExerciseSubstitution(Guid.NewGuid(), "Deadlift", "You've mastered the squat pattern.");
        var trainerPlan = RecommendationPolicy.TrainerPlanActive("Strength Foundations");

        foreach (var rec in new[] { volume, recovery, substitution, trainerPlan })
        {
            rec.Explanation.ShouldNotBeNullOrWhiteSpace();
            rec.Title.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
