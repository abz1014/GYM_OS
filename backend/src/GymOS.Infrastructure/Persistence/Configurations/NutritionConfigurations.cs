using GymOS.Domain.Nutrition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(150).IsRequired();
    }
}

public class DietPlanConfiguration : IEntityTypeConfiguration<DietPlan>
{
    public void Configure(EntityTypeBuilder<DietPlan> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.HasMany(p => p.MealEntries).WithOne(e => e.DietPlan).HasForeignKey(e => e.DietPlanId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Guidance).WithOne(g => g.DietPlan).HasForeignKey(g => g.DietPlanId).OnDelete(DeleteBehavior.Cascade);

        // Four handlers independently resolve "the plan active today" as
        // StartDate <= today && (EndDate == null || EndDate >= today), and there was no index behind
        // any of them.
        builder.HasIndex(p => new { p.MemberId, p.StartDate });
    }
}

public class DietPlanGuidanceConfiguration : IEntityTypeConfiguration<DietPlanGuidance>
{
    public void Configure(EntityTypeBuilder<DietPlanGuidance> builder)
    {
        builder.Property(g => g.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(g => new { g.DietPlanId, g.Cadence, g.EffectiveFrom });
    }
}

public class PlanAdherenceLogConfiguration : IEntityTypeConfiguration<PlanAdherenceLog>
{
    public void Configure(EntityTypeBuilder<PlanAdherenceLog> builder)
    {
        builder.Property(a => a.Note).HasMaxLength(500);

        /*
         * One tick per member per day, enforced by the schema rather than only by the handler.
         *
         * The command checks for an existing row first, but a double-tap on a slow connection races
         * that check — and the XP award is idempotent while the ROW is not, so the member would end
         * up with two adherence records for one day and a nutritionist reading a compliance figure
         * built on counting them. The unique index is what makes the second write impossible rather
         * than merely unlikely.
         */
        builder.HasIndex(a => new { a.MemberId, a.OnDate }).IsUnique();
    }
}

public class MealEntryConfiguration : IEntityTypeConfiguration<MealEntry>
{
    public void Configure(EntityTypeBuilder<MealEntry> builder)
    {
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(e => e.FoodItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
