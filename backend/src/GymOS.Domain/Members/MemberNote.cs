using GymOS.Domain.Common;

namespace GymOS.Domain.Members;

/// <summary>
/// A staff note on a member — "called about renewal", "asked to freeze in August", "brought a guest".
///
/// Separate from <see cref="MedicalNote"/> on purpose, and the distinction is not cosmetic. Medical
/// notes are health data: they carry a different retention expectation, a different disclosure risk,
/// and are already flagged as a compliance item before real client data loads. Routing an
/// operational note through that table would quietly reclassify "left a voicemail" as a medical
/// record, and — worse in the other direction — would put health information in front of every role
/// holding members.update.
///
/// <see cref="AuthorUserId"/> is required rather than nullable because the entire value of a note in
/// a shared inbox is knowing who left it; an anonymous note in a member's history is an argument
/// waiting to happen at the front desk.
/// </summary>
public class MemberNote : BaseEntity
{
    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string Note { get; set; } = string.Empty;

    /// <summary>The staff member who wrote it. Never null — see the remarks.</summary>
    public Guid AuthorUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
