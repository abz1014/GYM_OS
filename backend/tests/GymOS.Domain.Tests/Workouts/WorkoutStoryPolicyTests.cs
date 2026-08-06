using GymOS.Domain.Workouts;
using Shouldly;

namespace GymOS.Domain.Tests.Workouts;

/// <summary>
/// Saying what happened, in a member's words. Every test here defends the same line: the story is
/// only ever a rephrasing of facts the engine already holds, and it must never say something about
/// a member's training that the member could check and find untrue.
/// </summary>
public class WorkoutStoryPolicyTests
{
    private static RecordGain Gain(string exercise, decimal value, decimal? previous) =>
        new(exercise, value, "kg", previous);

    [Fact]
    public void A_session_with_no_details_says_so_without_implying_the_work_was_lost()
    {
        // "(nothing recorded)" reads as "I lost my workout". They trained; only the details are missing.
        var story = WorkoutStoryPolicy.Tell("Workout", [], []);

        story.Title.ShouldBe("Workout completed");
        story.Lines.ShouldHaveSingleItem().ShouldBe("No exercise details recorded.");
        story.OneLine.ShouldNotContain("nothing");
    }

    [Fact]
    public void An_ordinary_session_reads_as_what_was_done()
    {
        var story = WorkoutStoryPolicy.Tell("Chest", ["Bench Press 3×8", "Push-Up 3×12"], []);

        story.Title.ShouldBe("Chest");
        story.Lines.ShouldHaveSingleItem().ShouldBe("Bench Press 3×8, Push-Up 3×12.");
    }

    [Fact]
    public void A_long_session_names_a_few_movements_and_counts_the_rest()
    {
        var story = WorkoutStoryPolicy.Tell("Full body", ["A 3×8", "B 3×8", "C 3×8", "D 3×8", "E 3×8"], []);

        story.Lines[0].ShouldBe("A 3×8, B 3×8, C 3×8, and 2 more.");
    }

    [Fact]
    public void A_heavier_lift_is_reported_as_the_gain_it_was()
    {
        var story = WorkoutStoryPolicy.Tell("Chest", ["Bench Press 3×8"], [Gain("Bench Press", 62.5m, 60m)]);

        story.Lines[1].ShouldBe("Bench Press up 2.5kg to 62.5kg — a new best.");
    }

    [Fact]
    public void A_first_best_is_a_milestone_not_an_improvement()
    {
        // With nothing to beat there is no gain to claim, and inventing one would be checkable nonsense.
        var story = WorkoutStoryPolicy.Tell("Chest", ["Bench Press 3×8"], [Gain("Bench Press", 60m, null)]);

        story.Lines[1].ShouldBe("Your best Bench Press yet at 60kg.");
    }

    [Fact]
    public void One_lift_setting_several_records_at_once_is_still_one_sentence()
    {
        // Max weight, estimated 1RM and session volume all describe the same set of bench press.
        var story = WorkoutStoryPolicy.Tell("Chest", ["Bench Press 3×8"],
            [Gain("Bench Press", 62.5m, 60m), Gain("Bench Press", 78m, 75m), Gain("Bench Press", 1500m, 1400m)]);

        story.Lines.Count.ShouldBe(2);                       // the movements, then one record line
        story.Lines[1].ShouldContain("Bench Press");
    }

    [Fact]
    public void The_biggest_gain_leads()
    {
        var story = WorkoutStoryPolicy.Tell("Legs", ["Squat 5×5", "Leg Press 3×10"],
            [Gain("Leg Press", 102m, 100m), Gain("Squat", 110m, 100m)]);

        story.Lines[1].ShouldStartWith("Squat up 10kg");
        story.Lines[2].ShouldStartWith("Leg Press up 2kg");
    }

    [Fact]
    public void A_record_that_did_not_actually_beat_anything_is_not_called_a_gain()
    {
        // Equalling a best is worth reporting; describing it as an increase would not be true.
        var story = WorkoutStoryPolicy.Tell("Chest", ["Bench Press 3×8"], [Gain("Bench Press", 60m, 60m)]);

        story.Lines[1].ShouldBe("Your best Bench Press yet at 60kg.");
    }

    [Fact]
    public void Weights_read_the_way_a_member_would_write_them()
    {
        var story = WorkoutStoryPolicy.Tell("Chest", ["Bench Press 3×8"], [Gain("Bench Press", 60.00m, 57.50m)]);

        story.Lines[1].ShouldBe("Bench Press up 2.5kg to 60kg — a new best.");
    }

    [Fact]
    public void The_whole_story_collapses_to_one_line_where_there_is_only_room_for_a_sentence()
    {
        var story = WorkoutStoryPolicy.Tell("Chest", ["Bench Press 3×8"], [Gain("Bench Press", 62.5m, 60m)]);

        story.OneLine.ShouldBe("Bench Press 3×8. Bench Press up 2.5kg to 62.5kg — a new best.");
    }
}
