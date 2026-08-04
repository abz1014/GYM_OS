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
