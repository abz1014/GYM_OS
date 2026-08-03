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
    }
}

public class MealEntryConfiguration : IEntityTypeConfiguration<MealEntry>
{
    public void Configure(EntityTypeBuilder<MealEntry> builder)
    {
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(e => e.FoodItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
