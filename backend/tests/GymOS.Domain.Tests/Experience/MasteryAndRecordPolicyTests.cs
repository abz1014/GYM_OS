using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>Pins the pure workout-derived rules: 1RM estimation, per-session record aggregation, the
/// strict-greater PR rule (which is also what makes detection idempotent), and the bounded mastery
/// curve.</summary>
public class MasteryAndRecordPolicyTests
{
    [Theory]
    [InlineData(100, 0, 100)]    // no reps -> the weight itself
    [InlineData(100, 1, 103.33)] // 100 * (1 + 1/30)
    [InlineData(60, 10, 80)]     // 60 * (1 + 10/30)
    [InlineData(0, 5, 0)]
    public void OneRepMax_uses_Epley_with_a_sane_zero_rep_fallback(double weight, int reps, double expected)
        => OneRepMax.Epley((decimal)weight, reps).ShouldBe((decimal)expected);

    [Fact]
    public void StatsFor_aggregates_max_weight_best_1rm_and_total_volume_across_a_session()
    {
        // Two sets of the same exercise: 3x8@60 and 1x5@80.
        var stats = PersonalRecordPolicy.StatsFor([(3, 8, 60m), (1, 5, 80m)]);

        stats.MaxWeightKg.ShouldBe(80m);
        // best 1RM = max(Epley(60,8)=76, Epley(80,5)=93.33) = 93.33
        stats.BestEstimatedOneRepMax.ShouldBe(93.33m);
        // volume = 3*8*60 + 1*5*80 = 1440 + 400 = 1840
        stats.SessionVolume.ShouldBe(1840m);
    }

    [Fact]
    public void Beats_requires_strictly_greater_and_positive()
    {
        PersonalRecordPolicy.Beats(100, 90).ShouldBeTrue();
        PersonalRecordPolicy.Beats(100, 100).ShouldBeFalse(); // ties are not records -> idempotent re-detection
        PersonalRecordPolicy.Beats(90, 100).ShouldBeFalse();
        PersonalRecordPolicy.Beats(0, 0).ShouldBeFalse();      // an unweighted session never records
    }

    [Fact]
    public void ValueFor_maps_each_record_type_to_its_metric()
    {
        var stats = new ExerciseSessionStats(MaxWeightKg: 80, BestEstimatedOneRepMax: 93.33m, SessionVolume: 1840);

        PersonalRecordPolicy.ValueFor(stats, PersonalRecordType.MaxWeight).ShouldBe(80m);
        PersonalRecordPolicy.ValueFor(stats, PersonalRecordType.EstimatedOneRepMax).ShouldBe(93.33m);
        PersonalRecordPolicy.ValueFor(stats, PersonalRecordType.SessionVolume).ShouldBe(1840m);
    }

    [Fact]
    public void MasteryPercent_is_zero_at_the_start_bounded_at_100_and_monotonic()
    {
        MasteryPolicy.MasteryPercent(0, 0).ShouldBe(0);
        MasteryPolicy.MasteryPercent(1000, 10_000_000).ShouldBe(100); // both caps saturated

        var previous = -1;
        for (var sessions = 0; sessions <= 30; sessions++)
        {
            var value = MasteryPolicy.MasteryPercent(sessions, 0);
            value.ShouldBeGreaterThanOrEqualTo(previous);
            previous = value;
        }
    }

    [Fact]
    public void MasteryPercent_caps_the_session_and_volume_contributions_independently()
    {
        // Sessions cap: 20 sessions (3 pts each) = 60, and more sessions don't add beyond it.
        MasteryPolicy.MasteryPercent(20, 0).ShouldBe(60);
        MasteryPolicy.MasteryPercent(100, 0).ShouldBe(60);

        // Volume cap: 40,000 kg -> 40, alone.
        MasteryPolicy.MasteryPercent(0, 40_000).ShouldBe(40);
        MasteryPolicy.MasteryPercent(0, 999_999).ShouldBe(40);
    }
}
