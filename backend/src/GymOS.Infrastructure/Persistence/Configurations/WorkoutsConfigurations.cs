using GymOS.Domain.Workouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
    }
}

public class WorkoutTemplateConfiguration : IEntityTypeConfiguration<WorkoutTemplate>
{
    public void Configure(EntityTypeBuilder<WorkoutTemplate> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.HasMany(t => t.TemplateExercises).WithOne(te => te.WorkoutTemplate).HasForeignKey(te => te.WorkoutTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkoutTemplateExerciseConfiguration : IEntityTypeConfiguration<WorkoutTemplateExercise>
{
    public void Configure(EntityTypeBuilder<WorkoutTemplateExercise> builder)
    {
        builder.HasOne(te => te.Exercise).WithMany().HasForeignKey(te => te.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkoutLogConfiguration : IEntityTypeConfiguration<WorkoutLog>
{
    public void Configure(EntityTypeBuilder<WorkoutLog> builder)
    {
        builder.HasMany(l => l.Entries).WithOne(e => e.WorkoutLog).HasForeignKey(e => e.WorkoutLogId).OnDelete(DeleteBehavior.Cascade);
    }
}
