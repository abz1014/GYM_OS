using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// Skill-tree progression is pure: each node unlocks independently from the member's best-ever
/// single-set reps on its exercise, and the "next" recommendation is the node right after the
/// member's furthest-unlocked node — never the tree's untouched first node, never a backward step.
/// </summary>
public class SkillTreePolicyTests
{
    private static (Guid NodeId, Guid ExerciseId, int OrderIndex, int MinReps) Node(int order, Guid exerciseId, int minReps)
        => (Guid.NewGuid(), exerciseId, order, minReps);

    [Fact]
    public void EvaluateProgress_unlocks_each_node_independently_of_the_others()
    {
        var legPress = Guid.NewGuid();
        var squat = Guid.NewGuid();
        var deadlift = Guid.NewGuid();

        var nodes = new[] { Node(0, legPress, 10), Node(1, squat, 6), Node(2, deadlift, 5) };
        // Member never logged Leg Press or Deadlift, but has 6 reps on Squat.
        var bestReps = new Dictionary<Guid, int> { [squat] = 6 };

        var progress = SkillTreePolicy.EvaluateProgress(nodes, bestReps);

        progress.Count.ShouldBe(3);
        progress[0].Unlocked.ShouldBeFalse(); // Leg Press: never logged
        progress[1].Unlocked.ShouldBeTrue();  // Squat: 6 >= 6
        progress[2].Unlocked.ShouldBeFalse(); // Deadlift: never logged
    }

    [Fact]
    public void NextNode_recommends_the_node_after_the_furthest_unlocked_one()
    {
        var progress = new List<SkillNodeProgress>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 0, false),
            new(Guid.NewGuid(), Guid.NewGuid(), 1, true), // furthest unlocked
            new(Guid.NewGuid(), Guid.NewGuid(), 2, false),
        };

        var next = SkillTreePolicy.NextNode(progress);

        next.ShouldNotBeNull();
        next!.Value.OrderIndex.ShouldBe(2);
    }

    [Fact]
    public void NextNode_is_null_when_nothing_is_unlocked_yet()
    {
        var progress = new List<SkillNodeProgress>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 0, false),
            new(Guid.NewGuid(), Guid.NewGuid(), 1, false),
        };

        // Not the tree's first node — a member who hasn't engaged with this movement pattern at all
        // gets no cold-start recommendation.
        SkillTreePolicy.NextNode(progress).ShouldBeNull();
    }

    [Fact]
    public void NextNode_is_null_once_the_last_node_is_unlocked()
    {
        var progress = new List<SkillNodeProgress>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 0, true),
            new(Guid.NewGuid(), Guid.NewGuid(), 1, true),
        };

        SkillTreePolicy.NextNode(progress).ShouldBeNull();
    }
}
