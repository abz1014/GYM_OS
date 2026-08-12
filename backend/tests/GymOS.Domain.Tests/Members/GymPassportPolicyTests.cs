using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// The member's map of their own gym, drawn in body regions. The recurring point of these tests is
/// that an untried movement is a fact rather than a failure: it must be listed, counted honestly,
/// and never dressed up as a score the member is falling short of. The regions come from
/// MuscleGroupVocabulary — the same eight the picker and the body map use — so the map's shape is a
/// fixed geography rather than an accident of how tidily the Equipment field was typed.
/// </summary>
public class GymPassportPolicyTests
{
    private static readonly DateOnly Today = new(2026, 8, 6);

    private static PassportCatalogueEntry Ex(
        string name, string equipment, int sessions = 0, decimal best = 0, string? muscle = "Chest",
        int daysAgo = 5) =>
        new(Guid.NewGuid(), name, muscle, equipment, sessions, best,
            sessions > 0 ? Today.AddDays(-daysAgo) : null);

    [Fact]
    public void An_empty_catalogue_covers_nothing_without_dividing_by_zero()
    {
        var passport = GymPassportPolicy.Build([], Today);

        passport.Available.ShouldBe(0);
        passport.PercentCovered.ShouldBe(0);
        passport.Complete.ShouldBeFalse();     // nothing to complete is not completion
        passport.Stamps.ShouldBeEmpty();
    }

    [Fact]
    public void A_region_the_member_has_never_entered_is_still_on_the_map()
    {
        // The whole reason this exists: mastery is built from sessions, so an untouched corner of
        // the gym is invisible to it. Here it must appear.
        var passport = GymPassportPolicy.Build([
            Ex("Bench Press", "Barbell", sessions: 4, best: 60m),
            Ex("Leg Press", "Machine", muscle: "Legs"),
        ], Today);

        passport.Tried.ShouldBe(1);
        passport.Available.ShouldBe(2);
        passport.Stamps.Select(s => s.RegionKey).ShouldContain("legs");
        passport.Stamps.Single(s => s.RegionKey == "legs").Entries.Single().Tried.ShouldBeFalse();
    }

    [Fact]
    public void Coverage_is_counted_against_the_gyms_own_catalogue()
    {
        // A site that has no leg press has no leg press to cover — the denominator is theirs.
        var passport = GymPassportPolicy.Build([
            Ex("Bench Press", "Barbell", sessions: 2),
            Ex("Push-Up", "Bodyweight", sessions: 1),
            Ex("Plank", "Bodyweight"),
        ], Today);

        passport.Tried.ShouldBe(2);
        passport.Available.ShouldBe(3);
        passport.PercentCovered.ShouldBe(67);
    }

    [Fact]
    public void A_member_who_has_tried_one_of_many_is_never_shown_zero_percent()
    {
        var catalogue = Enumerable.Range(1, 60).Select(i => Ex($"Ex {i}", "Barbell", sessions: i == 1 ? 1 : 0)).ToList();

        GymPassportPolicy.Build(catalogue, Today).PercentCovered.ShouldBe(2);
    }

    [Fact]
    public void A_movement_carrying_no_load_reports_no_best_rather_than_zero_kilos()
    {
        // "0kg" on a plank reads as a failure. The number simply does not exist.
        var passport = GymPassportPolicy.Build([Ex("Plank", "Bodyweight", sessions: 5, best: 0m)], Today);

        var entry = passport.Stamps.Single().Entries.Single();
        entry.Tried.ShouldBeTrue();
        entry.BestWeightKg.ShouldBeNull();
    }

    [Fact]
    public void A_loaded_movement_reports_the_heaviest_they_have_managed()
    {
        var passport = GymPassportPolicy.Build([Ex("Bench Press", "Barbell", sessions: 9, best: 62.5m)], Today);

        passport.Stamps.Single().Entries.Single().BestWeightKg.ShouldBe(62.5m);
    }

    [Fact]
    public void Within_a_group_their_record_comes_before_what_is_left()
    {
        var passport = GymPassportPolicy.Build([
            Ex("Untried A", "Barbell"),
            Ex("Squat", "Barbell", sessions: 2),
            Ex("Deadlift", "Barbell", sessions: 7),
            Ex("Untried B", "Barbell"),
        ], Today);

        passport.Stamps.Single().Entries.Select(e => e.ExerciseName)
            .ShouldBe(["Deadlift", "Squat", "Untried A", "Untried B"]);
    }

    [Fact]
    public void Regions_read_in_body_order_not_usage_order()
    {
        // The map is a fixed geography — the same order the picker and the body map use — so it
        // reads the same way every visit. Usage-ordered groups reshuffled the page every week, and a
        // map whose territories move is not a map.
        var passport = GymPassportPolicy.Build([
            Ex("Treadmill Run", "Treadmill", sessions: 40, muscle: "Cardio"),
            Ex("Bench Press", "Barbell", sessions: 3),
            Ex("Squat", "Barbell", sessions: 3, muscle: "Legs"),
        ], Today);

        passport.Stamps.Select(s => s.RegionKey).ShouldBe(["chest", "legs", "cardio"]);
        passport.Stamps.First().Complete.ShouldBeTrue();
    }

    [Fact]
    public void Synonyms_fold_into_one_region_rather_than_two()
    {
        // "Quads" and "Legs" are the same territory. Two gyms typing differently must get one map —
        // the same rule the picker and the body map already live by, via the same vocabulary.
        var passport = GymPassportPolicy.Build([
            Ex("Squat", "Barbell", sessions: 2, muscle: "Legs"),
            Ex("Leg Extension", "Machine", muscle: "Quads"),
        ], Today);

        var legs = passport.Stamps.ShouldHaveSingleItem();
        legs.RegionKey.ShouldBe("legs");
        legs.RegionName.ShouldBe("Legs");
        legs.Available.ShouldBe(2);
        legs.Tried.ShouldBe(1);
    }

    [Fact]
    public void The_equipment_label_rides_along_on_each_entry()
    {
        // The region says where on the body; equipment says where in the building — the detail that
        // sends a member to the right corner for something they have never tried.
        var passport = GymPassportPolicy.Build([Ex("Leg Press", "Machine", muscle: "Legs")], Today);

        passport.Stamps.Single().Entries.Single().Equipment.ShouldBe("Machine");
    }

    [Fact]
    public void A_muscle_label_the_vocabulary_never_heard_of_lands_in_Other_rather_than_vanishing()
    {
        // Any gym's catalogue, however tidily it was filled in. "Adductors" is a real thing a gym
        // types; the map files it somewhere visible instead of dropping the movement from a screen
        // that claims to list the whole gym.
        var passport = GymPassportPolicy.Build([
            new(Guid.NewGuid(), "Mystery Machine", null, "Machine", 0, 0, null),
            new(Guid.NewGuid(), "Adductor Squeeze", "Adductors", "Machine", 1, 0, new DateOnly(2026, 8, 1)),
        ], Today);

        var stamp = passport.Stamps.ShouldHaveSingleItem();
        stamp.RegionKey.ShouldBe("other");
        stamp.Available.ShouldBe(2);
        stamp.Tried.ShouldBe(1);
    }

    [Fact]
    public void A_member_who_has_used_everything_is_told_so()
    {
        var passport = GymPassportPolicy.Build([
            Ex("Bench Press", "Barbell", sessions: 1),
            Ex("Leg Press", "Machine", sessions: 1, muscle: "Legs"),
        ], Today);

        passport.Complete.ShouldBeTrue();
        passport.PercentCovered.ShouldBe(100);
        passport.Stamps.ShouldAllBe(s => s.Complete);
    }

    [Fact]
    public void A_movement_left_alone_for_a_month_has_gone_quiet()
    {
        // The point of v2: at 100% coverage v1 has nothing left to say, yet a member can still have
        // quietly dropped something out of their training.
        var passport = GymPassportPolicy.Build([
            Ex("Bench Press", "Barbell", sessions: 9, daysAgo: 2),
            Ex("Overhead Press", "Barbell", sessions: 4, daysAgo: 41),
        ], Today);

        passport.Complete.ShouldBeTrue();
        passport.GoneQuiet.ShouldHaveSingleItem().ExerciseName.ShouldBe("Overhead Press");
    }

    [Fact]
    public void Something_trained_recently_has_not_gone_quiet()
    {
        var passport = GymPassportPolicy.Build([Ex("Bench Press", "Barbell", sessions: 3, daysAgo: 29)], Today);

        passport.GoneQuiet.ShouldBeEmpty();
        passport.Stamps.Single().Entries.Single().DaysSinceLastTrained.ShouldBe(29);
    }

    [Fact]
    public void Never_tried_is_not_the_same_as_gone_quiet()
    {
        // An untried movement is already reported by the coverage count; calling it neglected too
        // would say the same thing twice and imply they had abandoned something they never began.
        var passport = GymPassportPolicy.Build([Ex("Leg Press", "Machine")], Today);

        passport.GoneQuiet.ShouldBeEmpty();
        var entry = passport.Stamps.Single().Entries.Single();
        entry.Tried.ShouldBeFalse();
        entry.DaysSinceLastTrained.ShouldBeNull();
        entry.GoneQuiet.ShouldBeFalse();
    }

    [Fact]
    public void The_longest_neglected_are_named_first_and_the_list_stays_short()
    {
        // A hundred-movement gym would otherwise flag ninety things and mean nothing.
        var passport = GymPassportPolicy.Build([
            Ex("A", "Barbell", sessions: 1, daysAgo: 35),
            Ex("B", "Barbell", sessions: 1, daysAgo: 90),
            Ex("C", "Barbell", sessions: 1, daysAgo: 60),
            Ex("D", "Barbell", sessions: 1, daysAgo: 45),
            Ex("E", "Barbell", sessions: 1, daysAgo: 31),
        ], Today);

        passport.GoneQuiet.Count.ShouldBe(GymPassportPolicy.MaxQuietHighlighted);
        passport.GoneQuiet.Select(e => e.ExerciseName).ShouldBe(["B", "C", "D"]);
    }

    [Fact]
    public void A_record_dated_in_the_future_is_bad_data_not_negative_days()
    {
        var passport = GymPassportPolicy.Build([Ex("Bench Press", "Barbell", sessions: 1, daysAgo: -3)], Today);

        passport.Stamps.Single().Entries.Single().DaysSinceLastTrained.ShouldBe(0);
    }

    [Fact]
    public void A_brand_new_member_gets_the_whole_gym_to_look_at()
    {
        var passport = GymPassportPolicy.Build([
            Ex("Bench Press", "Barbell"),
            Ex("Leg Press", "Machine", muscle: "Legs"),
        ], Today);

        passport.Tried.ShouldBe(0);
        passport.PercentCovered.ShouldBe(0);
        passport.Available.ShouldBe(2);
        passport.Stamps.Count.ShouldBe(2);       // nothing is hidden for being untried
        passport.Complete.ShouldBeFalse();
    }
}
