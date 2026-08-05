using GymOS.Domain.Experience;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class MemberProgressionConfiguration : IEntityTypeConfiguration<MemberProgression>
{
    public void Configure(EntityTypeBuilder<MemberProgression> builder)
    {
        // Exactly one progression projection per member — the award handler upserts on this.
        builder.HasIndex(p => p.MemberId).IsUnique();
    }
}

public class XpTransactionConfiguration : IEntityTypeConfiguration<XpTransaction>
{
    public void Configure(EntityTypeBuilder<XpTransaction> builder)
    {
        // Backs both the per-member ledger read and the idempotency check
        // (member + source + reason must be unique — a source event is credited at most once).
        builder.HasIndex(t => new { t.MemberId, t.SourceType, t.SourceId, t.Reason });
    }
}

public class PersonalRecordConfiguration : IEntityTypeConfiguration<PersonalRecord>
{
    public void Configure(EntityTypeBuilder<PersonalRecord> builder)
    {
        // Backs the "prior best for this exercise+metric" lookup that drives PR detection and the reads.
        builder.HasIndex(r => new { r.MemberId, r.ExerciseId, r.Type });
    }
}

public class ExerciseMasteryConfiguration : IEntityTypeConfiguration<ExerciseMastery>
{
    public void Configure(EntityTypeBuilder<ExerciseMastery> builder)
    {
        // Exactly one mastery projection per member+exercise — the recompute upserts on this.
        builder.HasIndex(m => new { m.MemberId, m.ExerciseId }).IsUnique();
    }
}

public class MemberAchievementConfiguration : IEntityTypeConfiguration<MemberAchievement>
{
    public void Configure(EntityTypeBuilder<MemberAchievement> builder)
    {
        builder.Property(a => a.Code).HasMaxLength(60).IsRequired();
        // Unlock-once: a member can hold each achievement code at most one time.
        builder.HasIndex(a => new { a.MemberId, a.Code }).IsUnique();
    }
}

public class RecoveryLogConfiguration : IEntityTypeConfiguration<RecoveryLog>
{
    public void Configure(EntityTypeBuilder<RecoveryLog> builder)
    {
        builder.Property(r => r.Notes).HasMaxLength(500);
        // Backs the per-member recovery reads and the recent-rest-days signal the RecoveryPolicy uses.
        builder.HasIndex(r => new { r.MemberId, r.LoggedOn });
    }
}
