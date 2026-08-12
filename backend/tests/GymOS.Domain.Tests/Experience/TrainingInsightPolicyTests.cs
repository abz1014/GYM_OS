using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The one or two things worth saying today. Two properties are defended throughout: an absent
/// signal produces no insight rather than a hedge, and nothing is described in terms the data cannot
/// support.
/// </summary>
public class TrainingInsightPolicyTests
{
    private static TrainingInsightSignals Nothing =>
        new(null, null, null, null, null, [], null, null, 0, 0);

    [Fact]
    public void A_member_the_engine_knows_nothing_about_is_told_nothing()
    {
        TrainingInsightPolicy.Rank(Nothing).ShouldBeEmpty();
    }

    [Fact]
    public void At_most_two_are_ever_returned()
    {
        // A third is a report, and nobody reads a report before training.
        var everything = new TrainingInsightSignals(
            "Chest", "Trained 4 times this week.",
            "Bench Press", 62.5m,
            "Deadlift",
            [new QuietMovement("Lat Pulldown", "Back", 40)],
            "Back",
            "Squat", 10m, 6);

        TrainingInsightPolicy.Rank(everything).Count.ShouldBe(TrainingInsightPolicy.MaxInsights);
    }

    [Fact]
    public void Recovery_leads_because_it_is_the_only_one_that_changes_what_not_to_do()
    {
        var s = Nothing with
        {
            FatiguedMuscleGroup = "Chest",
            RecoveryReason = "Four chest sessions in seven days.",
            ReadyExerciseName = "Bench Press",
        };

        var first = TrainingInsightPolicy.Rank(s)[0];
        first.Kind.ShouldBe(TrainingInsightKind.RecoveryAlert);
        first.Title.ShouldBe("Chest needs a rest");
        first.Detail.ShouldBe("Four chest sessions in seven days.");
    }

    [Fact]
    public void A_lift_ready_to_go_up_names_the_weight_to_try()
    {
        var s = Nothing with { ReadyExerciseName = "Bench Press", ReadyNextWeightKg = 62.5m };

        TrainingInsightPolicy.Rank(s).ShouldHaveSingleItem().Detail.ShouldBe("You've held steady long enough. Try 62.5kg.");
    }

    [Fact]
    public void Without_a_suggested_weight_it_does_not_invent_one()
    {
        var s = Nothing with { ReadyExerciseName = "Bench Press", ReadyNextWeightKg = null };

        TrainingInsightPolicy.Rank(s).ShouldHaveSingleItem().Detail.ShouldNotContain("kg");
    }

    [Fact]
    public void A_comeback_is_something_dropped_that_is_also_the_weakest_area()
    {
        var s = Nothing with
        {
            GoneQuiet = [new QuietMovement("Lat Pulldown", "Back", 42)],
            WeakestMuscleGroup = "Back",
        };

        var insight = TrainingInsightPolicy.Rank(s).ShouldHaveSingleItem();
        insight.Kind.ShouldBe(TrainingInsightKind.Comeback);
        insight.Title.ShouldBe("Pick Lat Pulldown back up");
        insight.Detail.ShouldBe("6 weeks since your last one, and Back is your least-trained area.");
    }

    [Fact]
    public void Something_dropped_from_an_area_they_train_plenty_is_only_gone_quiet()
    {
        // The two facts have to actually coincide. Otherwise it is not a comeback, just a gap.
        var s = Nothing with
        {
            GoneQuiet = [new QuietMovement("Lat Pulldown", "Back", 42)],
            WeakestMuscleGroup = "Legs",
        };

        TrainingInsightPolicy.Rank(s).ShouldHaveSingleItem().Kind.ShouldBe(TrainingInsightKind.GoneQuiet);
    }

    [Fact]
    public void The_same_movement_is_never_reported_twice_under_two_headings()
    {
        var s = Nothing with
        {
            GoneQuiet = [new QuietMovement("Lat Pulldown", "Back", 42)],
            WeakestMuscleGroup = "Back",
        };

        var insights = TrainingInsightPolicy.Rank(s);
        insights.ShouldHaveSingleItem().Kind.ShouldBe(TrainingInsightKind.Comeback);
        insights.ShouldNotContain(i => i.Kind == TrainingInsightKind.GoneQuiet);
    }

    [Fact]
    public void A_comeback_outranks_an_eased_off_lift_because_resuming_it_moves_two_things()
    {
        var s = Nothing with
        {
            EasedOffExerciseName = "Deadlift",
            GoneQuiet = [new QuietMovement("Lat Pulldown", "Back", 42)],
            WeakestMuscleGroup = "Back",
        };

        TrainingInsightPolicy.Rank(s)[0].Kind.ShouldBe(TrainingInsightKind.Comeback);
    }

    [Fact]
    public void A_second_quiet_movement_can_still_be_reported_alongside_a_comeback()
    {
        var s = Nothing with
        {
            GoneQuiet = [new QuietMovement("Lat Pulldown", "Back", 42), new QuietMovement("Leg Curl", "Legs", 36)],
            WeakestMuscleGroup = "Back",
        };

        var kinds = TrainingInsightPolicy.Rank(s).Select(i => i.Kind).ToList();
        kinds.ShouldBe([TrainingInsightKind.Comeback, TrainingInsightKind.GoneQuiet]);
    }

    [Fact]
    public void A_group_being_told_to_rest_is_never_also_offered_as_a_comeback()
    {
        // Reachable in real data: one cardio movement hammered this week while another sat idle for
        // six weeks makes Cardio both fatigued and least-trained. Telling someone to rest a group and
        // train it in the same breath is worse than saying only one of the two.
        var s = Nothing with
        {
            FatiguedMuscleGroup = "Cardio",
            RecoveryReason = "Four cardio sessions in seven days.",
            GoneQuiet = [new QuietMovement("Treadmill Run", "Cardio", 45)],
            WeakestMuscleGroup = "Cardio",
        };

        var insights = TrainingInsightPolicy.Rank(s);

        insights.ShouldHaveSingleItem().Kind.ShouldBe(TrainingInsightKind.RecoveryAlert);
        insights.ShouldNotContain(i => i.Kind == TrainingInsightKind.Comeback);
    }

    [Fact]
    public void Nor_is_it_offered_as_a_plain_gone_quiet()
    {
        // Same contradiction, different heading.
        var s = Nothing with
        {
            FatiguedMuscleGroup = "Cardio",
            GoneQuiet = [new QuietMovement("Treadmill Run", "Cardio", 45)],
        };

        TrainingInsightPolicy.Rank(s).ShouldNotContain(i => i.Kind == TrainingInsightKind.GoneQuiet);
    }

    [Fact]
    public void A_comeback_in_a_different_group_from_the_rested_one_is_still_offered()
    {
        var s = Nothing with
        {
            FatiguedMuscleGroup = "Chest",
            GoneQuiet = [new QuietMovement("Treadmill Run", "Cardio", 45)],
            WeakestMuscleGroup = "Cardio",
        };

        TrainingInsightPolicy.Rank(s).Select(i => i.Kind)
            .ShouldBe([TrainingInsightKind.RecoveryAlert, TrainingInsightKind.Comeback]);
    }

    [Fact]
    public void Momentum_is_reported_as_the_weight_it_gained_and_nothing_more()
    {
        // "Unusually good" would need a distribution of other members to measure against, and a
        // beginner out-improves an advanced lifter regardless of effort. The gain is the fact.
        var s = Nothing with { MomentumExerciseName = "Squat", MomentumGainKg = 7.5m, MomentumWeeks = 4 };

        var insight = TrainingInsightPolicy.Rank(s).ShouldHaveSingleItem();
        insight.Kind.ShouldBe(TrainingInsightKind.Momentum);
        insight.Detail.ShouldBe("Up 7.5kg over 4 weeks.");
        insight.Detail.ShouldNotContain("unusual");
    }

    [Fact]
    public void A_gain_of_nothing_is_not_momentum()
    {
        var s = Nothing with { MomentumExerciseName = "Squat", MomentumGainKg = 0m, MomentumWeeks = 4 };

        TrainingInsightPolicy.Rank(s).ShouldBeEmpty();
    }

    [Fact]
    public void A_recovery_signal_with_no_explanation_still_says_something_usable()
    {
        var s = Nothing with { FatiguedMuscleGroup = "Chest", RecoveryReason = null };

        TrainingInsightPolicy.Rank(s).ShouldHaveSingleItem().Detail.ShouldBe("Train something else today.");
    }

    [Fact]
    public void One_week_is_singular()
    {
        var s = Nothing with { MomentumExerciseName = "Squat", MomentumGainKg = 2.5m, MomentumWeeks = 1 };

        TrainingInsightPolicy.Rank(s).ShouldHaveSingleItem().Detail.ShouldBe("Up 2.5kg over 1 week.");
    }
}
