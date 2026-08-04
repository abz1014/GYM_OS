using GymOS.Domain.Workouts;
using Shouldly;

namespace GymOS.Domain.Tests.Workouts;

public class ProgressiveOverloadPolicyTests
{
    [Fact]
    public void A_single_logged_session_is_insufficient_data()
    {
        ProgressiveOverloadPolicy.Evaluate([new ExercisePerformance(60m, 30)])
            .ShouldBe(OverloadSuggestion.InsufficientData);
    }

    [Fact]
    public void No_sessions_at_all_is_insufficient_data()
    {
        ProgressiveOverloadPolicy.Evaluate([]).ShouldBe(OverloadSuggestion.InsufficientData);
    }

    [Fact]
    public void Heavier_weight_than_last_session_means_already_progressing()
    {
        ExercisePerformance[] sessions = [new(60m, 30), new(62.5m, 30)];
        ProgressiveOverloadPolicy.Evaluate(sessions).ShouldBe(OverloadSuggestion.Progressing);
    }

    [Fact]
    public void Same_weight_but_more_reps_than_last_session_means_progressing()
    {
        ExercisePerformance[] sessions = [new(60m, 24), new(60m, 30)];
        ProgressiveOverloadPolicy.Evaluate(sessions).ShouldBe(OverloadSuggestion.Progressing);
    }

    [Fact]
    public void Identical_weight_and_reps_across_the_last_two_sessions_is_a_plateau()
    {
        ExercisePerformance[] sessions = [new(60m, 30), new(60m, 30)];
        ProgressiveOverloadPolicy.Evaluate(sessions).ShouldBe(OverloadSuggestion.ReadyToIncreaseWeight);
    }

    [Fact]
    public void A_plateau_after_a_longer_history_of_progress_is_still_a_plateau()
    {
        // Only the last two sessions matter — earlier progress doesn't mask a fresh plateau.
        ExercisePerformance[] sessions = [new(50m, 24), new(55m, 24), new(60m, 30), new(60m, 30)];
        ProgressiveOverloadPolicy.Evaluate(sessions).ShouldBe(OverloadSuggestion.ReadyToIncreaseWeight);
    }

    [Fact]
    public void Lighter_weight_than_last_session_suggests_a_deload()
    {
        ExercisePerformance[] sessions = [new(60m, 30), new(55m, 30)];
        ProgressiveOverloadPolicy.Evaluate(sessions).ShouldBe(OverloadSuggestion.ConsiderDeload);
    }

    [Fact]
    public void Same_weight_but_fewer_reps_suggests_a_deload()
    {
        ExercisePerformance[] sessions = [new(60m, 30), new(60m, 20)];
        ProgressiveOverloadPolicy.Evaluate(sessions).ShouldBe(OverloadSuggestion.ConsiderDeload);
    }

    [Fact]
    public void Suggested_next_weight_rounds_to_the_nearest_half_kilogram()
    {
        ProgressiveOverloadPolicy.SuggestedNextWeightKg(60m).ShouldBe(61.5m); // 60 * 1.025 = 61.5 exactly
        ProgressiveOverloadPolicy.SuggestedNextWeightKg(20m).ShouldBe(20.5m); // 20 * 1.025 = 20.5 exactly
        ProgressiveOverloadPolicy.SuggestedNextWeightKg(45m).ShouldBe(46.0m); // 45 * 1.025 = 46.125 -> rounds to 46.0
    }
}
