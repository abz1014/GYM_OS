namespace GymOS.Domain.Experience;

/// <summary>
/// The pure rules of the XP engine — award amounts per reason and the level curve. No I/O, so the
/// whole progression system is unit-testable and identical whether it runs in an event handler, a
/// rebuild job, or a test. Award values weight consistency/verification/goal-completion above a bare
/// workout (a raw logged lift is <see cref="AwardFor"/>(WorkoutCompleted); trainer verification and
/// goal completion are worth more), encoding "never reward unsafe lifting alone".
/// </summary>
public static class XpPolicy
{
    public static int AwardFor(XpReason reason) => reason switch
    {
        XpReason.GymVisit => 20,
        XpReason.WorkoutCompleted => 50,
        XpReason.RecoveryLogged => 10,
        XpReason.NutritionAdherence => 15,
        XpReason.ProgressiveImprovement => 30,
        XpReason.StreakMilestone => 40,
        XpReason.TrainerVerified => 60,
        XpReason.GoalCompleted => 100,
        XpReason.ChallengeCompleted => 150,
        _ => 0
    };

    /// <summary>
    /// Cumulative XP required to *reach* a given level. Level 1 starts at 0; advancing from level L to
    /// L+1 costs 100·L, so the curve gets gently steeper (level 2 at 100, 3 at 300, 4 at 600, 5 at
    /// 1000, …). Closed form: 50·(L-1)·L.
    /// </summary>
    public static long CumulativeXpForLevel(int level)
    {
        if (level <= 1)
        {
            return 0;
        }

        return 50L * (level - 1) * level;
    }

    /// <summary>
    /// Resolves a total XP balance into the current level plus progress within it. XpForNextLevel is
    /// the size of the current level's band (100·level); it is 0 only for a negative balance, which
    /// is clamped to level 1.
    /// </summary>
    public static (int Level, long XpIntoLevel, long XpForNextLevel) LevelForXp(long totalXp)
    {
        if (totalXp <= 0)
        {
            return (1, 0, CumulativeXpForLevel(2));
        }

        var level = 1;
        while (CumulativeXpForLevel(level + 1) <= totalXp)
        {
            level++;
        }

        var xpIntoLevel = totalXp - CumulativeXpForLevel(level);
        var xpForNextLevel = CumulativeXpForLevel(level + 1) - CumulativeXpForLevel(level);
        return (level, xpIntoLevel, xpForNextLevel);
    }
}
