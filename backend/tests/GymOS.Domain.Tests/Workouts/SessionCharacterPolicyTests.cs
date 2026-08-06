using GymOS.Domain.Workouts;
using Shouldly;

namespace GymOS.Domain.Tests.Workouts;

/// <summary>
/// Naming a session from what was lifted. The rule under test throughout is that the name must be
/// true: a member shown "Push day" for a session containing curls has been told something false
/// about their own training, which is worse than the plain "Workout" it replaced.
/// </summary>
public class SessionCharacterPolicyTests
{
    [Fact]
    public void A_single_muscle_group_is_named_outright()
    {
        // "Back" is more specific than "Pull day" and cannot be wrong, so it wins.
        SessionCharacterPolicy.Describe(["Back", "Back", "Back"]).ShouldBe("Back");
    }

    [Fact]
    public void Several_groups_that_all_push_become_a_push_day()
    {
        SessionCharacterPolicy.Describe(["Chest", "Shoulders", "Triceps"]).ShouldBe("Push day");
    }

    [Fact]
    public void Several_groups_that_all_pull_become_a_pull_day()
    {
        SessionCharacterPolicy.Describe(["Back", "Biceps"]).ShouldBe("Pull day");
    }

    [Fact]
    public void A_group_that_spans_both_directions_is_never_forced_into_one()
    {
        // "Arms" covers a curl and a pushdown at once. Calling this a push day would be a guess, and
        // the gym's own words are available and accurate.
        SessionCharacterPolicy.Describe(["Chest", "Arms"]).ShouldBe("Arms & Chest");
    }

    [Fact]
    public void The_group_trained_most_leads_the_name()
    {
        SessionCharacterPolicy.Describe(["Arms", "Back", "Back", "Back"]).ShouldBe("Back & Arms");
    }

    [Fact]
    public void Equally_trained_groups_are_named_in_a_stable_order()
    {
        // The same session read twice must not produce two different names.
        var first = SessionCharacterPolicy.Describe(["Shoulders", "Core"]);
        var second = SessionCharacterPolicy.Describe(["Core", "Shoulders"]);
        first.ShouldBe(second);
        first.ShouldBe("Core & Shoulders");
    }

    [Fact]
    public void Enough_different_groups_stops_being_about_any_of_them()
    {
        SessionCharacterPolicy.Describe(["Chest", "Back", "Legs", "Core"]).ShouldBe("Full body");
    }

    [Fact]
    public void Three_groups_are_still_worth_listing()
    {
        SessionCharacterPolicy.Describe(["Chest", "Back", "Legs"]).ShouldBe("Back, Chest & Legs");
    }

    [Fact]
    public void Vocabulary_a_gym_happens_to_use_is_matched_regardless_of_case()
    {
        SessionCharacterPolicy.Describe(["CHEST", "delts"]).ShouldBe("Push day");
    }

    [Fact]
    public void Muscle_groups_this_product_has_never_heard_of_keep_their_own_names()
    {
        // Any gym's catalogue, in any language — an unrecognised label is reported, not categorised.
        SessionCharacterPolicy.Describe(["Rücken", "Brust"]).ShouldBe("Brust & Rücken");
    }

    [Fact]
    public void A_session_with_nothing_recorded_against_it_is_just_a_workout()
    {
        SessionCharacterPolicy.Describe([]).ShouldBe("Workout");
        SessionCharacterPolicy.Describe([null, "", "   "]).ShouldBe("Workout");
    }

    [Fact]
    public void The_same_group_written_two_ways_is_one_group()
    {
        SessionCharacterPolicy.Describe(["Legs", "legs", " Legs "]).ShouldBe("Legs");
    }
}
