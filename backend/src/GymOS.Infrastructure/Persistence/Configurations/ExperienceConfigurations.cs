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
