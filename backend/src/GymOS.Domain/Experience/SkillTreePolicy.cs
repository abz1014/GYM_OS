namespace GymOS.Domain.Experience;

/// <summary>One node's progress: whether the member has met its rep threshold, independent of any
/// other node in the tree (a member can unlock node 3 without ever having logged node 1).</summary>
public readonly record struct SkillNodeProgress(Guid NodeId, Guid ExerciseId, int OrderIndex, bool Unlocked);

/// <summary>
/// Pure policy for skill-tree progression (blueprint Phase 7). Each node unlocks independently from
/// the member's best-ever single-set rep count on its exercise — no "clear node 1 to attempt node 2"
/// gating, since a member's real history rarely lines up with a tidy sequence. The "next" recommended
/// node is simply the one after the member's furthest-unlocked node: reflects genuine progress rather
/// than always recommending the tree's first (possibly untouched) node.
/// </summary>
public static class SkillTreePolicy
{
    public static IReadOnlyList<SkillNodeProgress> EvaluateProgress(
        IReadOnlyList<(Guid NodeId, Guid ExerciseId, int OrderIndex, int MinReps)> nodes,
        IReadOnlyDictionary<Guid, int> bestRepsByExercise)
        => nodes
            .OrderBy(n => n.OrderIndex)
            .Select(n => new SkillNodeProgress(n.NodeId, n.ExerciseId, n.OrderIndex,
                bestRepsByExercise.GetValueOrDefault(n.ExerciseId, 0) >= n.MinReps))
            .ToList();

    /// <summary>
    /// The node immediately after the member's furthest-unlocked node (progress must already be
    /// ordered by OrderIndex — the shape EvaluateProgress returns). Null when nothing is unlocked yet
    /// (the member hasn't engaged with this tree — recommending its first, untouched node would be a
    /// cold-start guess, not a "you're ready for this" nudge) or the furthest-unlocked node is already
    /// the last one (fully progressed, nothing further to suggest).
    /// </summary>
    public static SkillNodeProgress? NextNode(IReadOnlyList<SkillNodeProgress> progressOrderedByOrderIndex)
    {
        var furthestUnlockedIndex = -1;
        for (var i = 0; i < progressOrderedByOrderIndex.Count; i++)
        {
            if (progressOrderedByOrderIndex[i].Unlocked)
            {
                furthestUnlockedIndex = i;
            }
        }

        if (furthestUnlockedIndex < 0 || furthestUnlockedIndex == progressOrderedByOrderIndex.Count - 1)
        {
            return null;
        }

        return progressOrderedByOrderIndex[furthestUnlockedIndex + 1];
    }
}
