using GymOS.Domain.Workouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();

        // Cascade: a muscle row describes its exercise and has no meaning without it. Orphans here
        // would be worse than useless — the recovery map reads this table, so a row pointing at a
        // deleted movement would keep a muscle group looking worked forever.
        builder.HasMany(e => e.Muscles).WithOne(m => m.Exercise).HasForeignKey(m => m.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExerciseMuscleConfiguration : IEntityTypeConfiguration<ExerciseMuscle>
{
    public void Configure(EntityTypeBuilder<ExerciseMuscle> builder)
    {
        // The canonical vocabulary key, never the gym's free text — see ExerciseMuscle.
        builder.Property(m => m.MuscleGroupKey).HasMaxLength(40).IsRequired();

        // Stored as text, like every other enum in this schema: a migration that reorders an enum
        // must not silently repaint every row's meaning.
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        // One row per (exercise, group). A movement cannot work its chest twice, and without this a
        // re-run seeder or a double-submitted edit would double the group's weight in every read
        // that counts rows.
        builder.HasIndex(m => new { m.ExerciseId, m.MuscleGroupKey }).IsUnique();
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

        // Restrict, mirroring WorkoutTemplateExercise: retiring a template must never take the
        // sessions performed against it with it. Until now WorkoutTemplateId was a bare Guid with no
        // constraint at all, so nothing stopped a log pointing at a template that no longer existed.
        builder.HasOne(l => l.WorkoutTemplate).WithMany().HasForeignKey(l => l.WorkoutTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkoutLogEntryConfiguration : IEntityTypeConfiguration<WorkoutLogEntry>
{
    public void Configure(EntityTypeBuilder<WorkoutLogEntry> builder)
    {
        // Same rule, same reason: an exercise that has ever been logged is part of somebody's
        // history and cannot be deleted out from under it.
        builder.HasOne(e => e.Exercise).WithMany().HasForeignKey(e => e.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
