namespace GymOS.Application.Modules.Experience.Dtos;

/// <summary>The member's progression snapshot: current level, total XP, progress within the current
/// level, and their most recent ledger entries (for a "recent activity" strip).</summary>
public record MyExperienceDto(
    int Level,
    long TotalXp,
    long XpIntoLevel,
    long XpForNextLevel,
    IReadOnlyList<MyXpEntryDto> Recent);

public record MyXpEntryDto(int Amount, string Reason, DateTimeOffset OccurredAt);
