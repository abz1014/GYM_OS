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

public class SkillTreeConfiguration : IEntityTypeConfiguration<SkillTree>
{
    public void Configure(EntityTypeBuilder<SkillTree> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.MuscleGroup).HasMaxLength(60);
    }
}

public class SkillNodeConfiguration : IEntityTypeConfiguration<SkillNode>
{
    public void Configure(EntityTypeBuilder<SkillNode> builder)
    {
        builder.Property(n => n.UnlockExplanation).HasMaxLength(500).IsRequired();
        // Backs "load this tree's nodes in order" — every read walks a tree front to back.
        builder.HasIndex(n => new { n.SkillTreeId, n.OrderIndex });
    }
}

public class CommunityChallengeConfiguration : IEntityTypeConfiguration<CommunityChallenge>
{
    public void Configure(EntityTypeBuilder<CommunityChallenge> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1000);
    }
}

public class ChallengeParticipantConfiguration : IEntityTypeConfiguration<ChallengeParticipant>
{
    public void Configure(EntityTypeBuilder<ChallengeParticipant> builder)
    {
        // One join per member per challenge — backs both the uniqueness rule and "is this member in
        // this challenge" reads.
        builder.HasIndex(p => new { p.ChallengeId, p.MemberId }).IsUnique();
    }
}

public class RankPromotionConfiguration : IEntityTypeConfiguration<RankPromotion>
{
    public void Configure(EntityTypeBuilder<RankPromotion> builder)
    {
        // The idempotency guarantee, enforced by the database rather than by a check-then-insert in
        // the handler. A tier is reached exactly once in a member's life because PeakXp ratchets, so
        // the handler can run on every XP award and simply insert what is missing — two concurrent
        // awards racing to record the same promotion collide here instead of double-celebrating.
        builder.HasIndex(r => new { r.MemberId, r.Tier }).IsUnique();

        // The unseen queue is read on every portal load; this keeps it off a table scan as the record
        // grows with every member's whole history.
        builder.HasIndex(r => new { r.MemberId, r.Seen });
    }
}
