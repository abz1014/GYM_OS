using GymOS.Domain.Experience;
using GymOS.Domain.Workouts;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The recommendation engine is pure, so each nudge's trigger condition is pinned here: plateaus only
/// promote the ReadyToIncreaseWeight signal, weekly focus picks the true weakest group, volume trend
/// only fires on a meaningful swing, recovery advice only fires when action is warranted, and every
/// recommendation carries a non-empty explanation ("always explain").
/// </summary>
public class RecommendationPolicyTests
{
    [Fact]
    public void PlateauAlerts_only_promotes_ReadyToIncreaseWeight_signals()
    {
        var exerciseId = Guid.NewGuid();
        var signals = new List<ExerciseOverloadSignal>
        {
            new(exerciseId, "Bench Press", OverloadSuggestion.ReadyToIncreaseWeight, 60m),
            new(Guid.NewGuid(), "Barbell Squat", OverloadSuggestion.Progressing, 85m),
            new(Guid.NewGuid(), "Deadlift", OverloadSuggestion.ConsiderDeload, 100m),
            new(Guid.NewGuid(), "Pull-Up", OverloadSuggestion.InsufficientData, null),
        };

        var alerts = RecommendationPolicy.PlateauAlerts(signals);

        alerts.ShouldHaveSingleItem();
        alerts[0].Type.ShouldBe(RecommendationType.PlateauAlert);
        alerts[0].ExerciseId.ShouldBe(exerciseId);
        alerts[0].Explanation.ShouldContain("Bench Press");
    }

    [Fact]
    public void WeeklyFocus_picks_the_lowest_mastery_group()
    {
        var groups = new List<MuscleGroupSignal> { new("Legs", 40), new("Chest", 8), new("Back", 25) };

        var focus = RecommendationPolicy.WeeklyFocus(groups);

        focus.ShouldNotBeNull();
        focus!.Title.ShouldContain("Chest");
        focus.Explanation.ShouldContain("8%");
    }

    [Fact]
    public void WeeklyFocus_is_null_with_no_mastery_data()
        => RecommendationPolicy.WeeklyFocus([]).ShouldBeNull();

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
        var plateau = RecommendationPolicy.PlateauAlerts(
            [new ExerciseOverloadSignal(Guid.NewGuid(), "Bench Press", OverloadSuggestion.ReadyToIncreaseWeight, 60m)])[0];
        var focus = RecommendationPolicy.WeeklyFocus([new MuscleGroupSignal("Chest", 8)])!;
        var volume = RecommendationPolicy.VolumeTrend(1000, 2000)!;
        var recovery = RecommendationPolicy.RecoveryAdvice(RecoveryStatus.Fatigued, "trained hard all week")!;
        var substitution = RecommendationPolicy.ExerciseSubstitution(Guid.NewGuid(), "Deadlift", "You've mastered the squat pattern.");
        var trainerPlan = RecommendationPolicy.TrainerPlanActive("Strength Foundations");

        foreach (var rec in new[] { plateau, focus, volume, recovery, substitution, trainerPlan })
        {
            rec.Explanation.ShouldNotBeNullOrWhiteSpace();
            rec.Title.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
