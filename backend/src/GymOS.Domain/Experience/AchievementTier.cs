namespace GymOS.Domain.Experience;

/// <summary>The badge tier of an achievement — its visual weight, from a first-step Bronze to a
/// Platinum milestone. (The blueprint's "Badge" is represented by this tier + the achievement's icon,
/// rather than a separate entity.)</summary>
public enum AchievementTier
{
    Bronze,
    Silver,
    Gold,
    Platinum
}
