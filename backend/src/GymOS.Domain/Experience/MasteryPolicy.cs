namespace GymOS.Domain.Experience;

/// <summary>
/// Turns accumulated training into a bounded 0–100 mastery signal. Consistency (number of sessions)
/// dominates and depth (total volume) tops it up, both with hard caps so mastery climbs steadily and
/// plateaus rather than running away — a beginner reads low, a member who trains a lift regularly
/// approaches 100. Pure, so it's identical for an exercise, a muscle group, or a machine (each just
/// passes its aggregated sessions/volume).
/// </summary>
public static class MasteryPolicy
{
    private const int SessionCap = 60;   // reached at 20 sessions (3 pts each)
    private const int VolumeCap = 40;    // reached at 40,000 kg of cumulative volume

    public static int MasteryPercent(int sessions, decimal totalVolume)
    {
        var fromSessions = Math.Min(SessionCap, Math.Max(0, sessions) * 3);
        var fromVolume = Math.Min(VolumeCap, (int)(Math.Max(0, totalVolume) / 1000m));
        return Math.Min(100, fromSessions + fromVolume);
    }
}
