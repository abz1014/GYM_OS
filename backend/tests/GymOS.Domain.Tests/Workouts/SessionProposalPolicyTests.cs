using GymOS.Domain.Workouts;
using Shouldly;

namespace GymOS.Domain.Tests.Workouts;

/// <summary>
/// The one-tap proposal. What matters here is that the majority path works for members with no
/// trainer (most of a gym), that nothing is invented, and that a first session is still one tap.
/// </summary>
public class SessionProposalPolicyTests
{
    private static Guid Ex(int n) => new($"{n:D8}-0000-0000-0000-000000000000");

    private static readonly IReadOnlyDictionary<Guid, decimal> NoWeights = new Dictionary<Guid, decimal>();
    private static readonly IReadOnlyList<PlannedExercise> NoPlan = [];
    private static readonly IReadOnlyList<ProposedEntry> NoHistory = [];
    private static readonly IReadOnlyList<PlannedExercise> NoCatalogue = [];

    [Fact]
    public void A_trainer_plan_wins_when_one_is_active()
    {
        var plan = new List<PlannedExercise> { new(Ex(1), "Bench Press", 3, 10) };
        var lastSession = new List<ProposedEntry> { new(Ex(2), "Barbell Squat", 4, 6, 90m) };

        var proposal = SessionProposalPolicy.Propose(plan, lastSession, NoWeights, NoCatalogue);

        proposal.Source.ShouldBe(SessionProposalSource.TrainerPlan);
        proposal.Entries.ShouldHaveSingleItem().ExerciseName.ShouldBe("Bench Press");
    }

    [Fact]
    public void The_plan_supplies_sets_and_reps_and_memory_supplies_the_weight()
    {
        // Templates store no load, so without remembered weights a plan alone can't fill a session.
        var plan = new List<PlannedExercise> { new(Ex(1), "Bench Press", 3, 10) };
        var remembered = new Dictionary<Guid, decimal> { [Ex(1)] = 60m };

        var entry = SessionProposalPolicy.Propose(plan, NoHistory, remembered, NoCatalogue).Entries.Single();

        entry.Sets.ShouldBe(3);
        entry.Reps.ShouldBe(10);
        entry.WeightKg.ShouldBe(60m);
    }

    [Fact]
    public void An_unknown_load_is_left_blank_rather_than_guessed()
    {
        var plan = new List<PlannedExercise> { new(Ex(1), "Bench Press", 3, 10) };

        var entry = SessionProposalPolicy.Propose(plan, NoHistory, NoWeights, NoCatalogue).Entries.Single();

        entry.WeightKg.ShouldBeNull();
        // Still confirmable — a member can accept the session and correct the load if they want to.
        entry.Sets.ShouldBe(3);
    }

    [Fact]
    public void Without_a_plan_the_majority_path_is_repeating_the_last_session()
    {
        // Most gym members have no trainer, so this is the main path, not a fallback.
        var lastSession = new List<ProposedEntry>
        {
            new(Ex(1), "Deadlift", 3, 8, 140m),
            new(Ex(2), "Pull-Up", 3, 10, null)
        };

        var proposal = SessionProposalPolicy.Propose(NoPlan, lastSession, NoWeights, NoCatalogue);

        proposal.Source.ShouldBe(SessionProposalSource.RepeatLast);
        proposal.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public void Repeating_a_session_reuses_exactly_what_was_lifted()
    {
        var lastSession = new List<ProposedEntry> { new(Ex(1), "Deadlift", 5, 5, 142.5m) };

        var entry = SessionProposalPolicy.Propose(NoPlan, lastSession, NoWeights, NoCatalogue).Entries.Single();

        entry.Sets.ShouldBe(5);
        entry.Reps.ShouldBe(5);
        entry.WeightKg.ShouldBe(142.5m);   // not rounded, not "improved" — what they actually did
    }

    [Fact]
    public void A_bodyweight_movement_carries_no_load_through_either_path()
    {
        var lastSession = new List<ProposedEntry> { new(Ex(2), "Pull-Up", 3, 10, null) };

        SessionProposalPolicy.Propose(NoPlan, lastSession, NoWeights, NoCatalogue)
            .Entries.Single().WeightKg.ShouldBeNull();
    }

    [Fact]
    public void A_first_timer_still_gets_one_tap()
    {
        var catalogue = new List<PlannedExercise>
        {
            new(Ex(1), "Barbell Squat", 3, 10),
            new(Ex(2), "Bench Press", 3, 10),
            new(Ex(3), "Lat Pulldown", 3, 10),
            new(Ex(4), "Plank", 3, 30),
        };

        var proposal = SessionProposalPolicy.Propose(NoPlan, NoHistory, NoWeights, catalogue);

        proposal.Source.ShouldBe(SessionProposalSource.Starter);
        proposal.Entries.Count.ShouldBe(SessionProposalPolicy.StarterExerciseCount);
        proposal.CanConfirm.ShouldBeTrue();
    }

    [Fact]
    public void A_short_catalogue_is_used_whole_rather_than_padded()
    {
        var catalogue = new List<PlannedExercise> { new(Ex(1), "Push-Up", 3, 12) };

        SessionProposalPolicy.Propose(NoPlan, NoHistory, NoWeights, catalogue)
            .Entries.Count.ShouldBe(1);
    }

    [Fact]
    public void With_nothing_at_all_there_is_nothing_to_confirm()
    {
        var proposal = SessionProposalPolicy.Propose(NoPlan, NoHistory, NoWeights, NoCatalogue);

        proposal.Source.ShouldBe(SessionProposalSource.None);
        proposal.CanConfirm.ShouldBeFalse();
        proposal.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_plan_falls_through_to_history_rather_than_proposing_nothing()
    {
        // A member whose plan has ended shouldn't be dropped back to an empty form.
        var lastSession = new List<ProposedEntry> { new(Ex(1), "Deadlift", 3, 8, 100m) };

        SessionProposalPolicy.Propose(NoPlan, lastSession, NoWeights, [])
            .Source.ShouldBe(SessionProposalSource.RepeatLast);
    }
}
