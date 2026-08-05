using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The recovery heuristic is pure, so its thresholds are pinned here: what training load reads as
/// Fresh / Ready / Fatigued / OvertrainingRisk overall and per muscle group, and — importantly — that
/// a logged rest day actually pulls a fatigued member back toward Ready.
/// </summary>
public class RecoveryPolicyTests
{
    [Theory]
    // No history, or a long layoff, reads as fully rested.
    [InlineData(0, 0, null, RecoveryStatus.Fresh)]
    [InlineData(2, 0, 3, RecoveryStatus.Fresh)]
    [InlineData(6, 0, 5, RecoveryStatus.Fresh)]   // days-off dominates last week's volume
    // Balanced recent load.
    [InlineData(2, 0, 1, RecoveryStatus.Ready)]
    [InlineData(3, 1, 0, RecoveryStatus.Ready)]
    // High load, or moderately high with no rest, reads as fatigued.
    [InlineData(5, 2, 1, RecoveryStatus.Fatigued)]
    [InlineData(4, 0, 0, RecoveryStatus.Fatigued)]
    // Near-daily with zero rest is the overtraining signal.
    [InlineData(6, 0, 0, RecoveryStatus.OvertrainingRisk)]
    [InlineData(7, 0, 1, RecoveryStatus.OvertrainingRisk)]
    public void ClassifyOverall_maps_load_to_status(int sessions, int rest, int? daysSince, RecoveryStatus expected)
    {
        var (status, reason) = RecoveryPolicy.ClassifyOverall(
            new RecoveryPolicy.RecoverySignals(sessions, rest, daysSince));

        status.ShouldBe(expected);
        reason.ShouldNotBeNullOrWhiteSpace(); // every status carries a plain-language explanation
    }

    [Fact]
    public void A_logged_rest_day_pulls_a_fatigued_member_back_to_ready()
    {
        // Four sessions in a week with no rest reads as Fatigued...
        RecoveryPolicy.ClassifyOverall(new RecoveryPolicy.RecoverySignals(4, 0, 1)).Status
            .ShouldBe(RecoveryStatus.Fatigued);

        // ...logging one recovery day that same week pulls it back to Ready — the reward for logging
        // recovery and the recovery status reinforce the same habit.
        RecoveryPolicy.ClassifyOverall(new RecoveryPolicy.RecoverySignals(4, 1, 1)).Status
            .ShouldBe(RecoveryStatus.Ready);
    }

    [Theory]
    [InlineData(4, 1, RecoveryStatus.OvertrainingRisk)] // hammered 4× this week
    [InlineData(1, 5, RecoveryStatus.Fresh)]            // not touched in 5 days
    [InlineData(2, 0, RecoveryStatus.Fatigued)]         // trained today, still recovering
    [InlineData(2, 2, RecoveryStatus.Ready)]            // 2 days rest, moderate frequency
    public void ClassifyMuscleGroup_maps_recency_and_frequency(int times, int? daysSince, RecoveryStatus expected)
    {
        var (status, reason) = RecoveryPolicy.ClassifyMuscleGroup(
            new RecoveryPolicy.MuscleRecoverySignals("Chest", times, daysSince));

        status.ShouldBe(expected);
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_group_with_no_history_is_fresh()
        => RecoveryPolicy.ClassifyMuscleGroup(new RecoveryPolicy.MuscleRecoverySignals("Back", 0, null)).Status
            .ShouldBe(RecoveryStatus.Fresh);
}
