namespace GymOS.Domain.Workouts;

/// <summary>
/// How long a member has to take back a session they just recorded.
///
/// One-tap confirmation makes an accidental tap cheap, and an accidental tap is expensive: it mints
/// XP, can set a personal record that never happened, and inflates the streak the whole product hangs
/// on. Undo is what keeps that data honest, so it exists for exactly the mistakes the one-tap flow
/// makes possible — a mis-tap noticed immediately — rather than as general-purpose history editing.
///
/// The window is short on purpose. Long enough to catch "that wasn't me" while the member is still
/// holding the phone; short enough that a member cannot quietly rewrite last week to game a
/// leaderboard, and short enough that undo never becomes the way people correct old data.
/// </summary>
public static class WorkoutUndoPolicy
{
    public static readonly TimeSpan UndoWindow = TimeSpan.FromMinutes(30);

    /// <summary>Whether a session recorded at <paramref name="loggedAt"/> can still be taken back.</summary>
    public static bool CanUndo(DateTimeOffset loggedAt, DateTimeOffset now) => now - loggedAt <= UndoWindow;
}
