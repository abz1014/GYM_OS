using GymOS.Domain.Classes;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Infrastructure.Seeding;

public partial class DemoDataSeeder
{
    /// <summary>
    /// Seeds a realistic weekly group-class timetable for the primary branch, then pre-materialises
    /// the first booking window of concrete sessions (same ClassSessionPlanner the runtime job uses),
    /// so a fresh demo shows a populated calendar immediately rather than waiting for the nightly job.
    /// </summary>
    private async Task SeedClassesAsync(Guid tenantId, List<Branch> branches, List<Trainer> trainers, CancellationToken cancellationToken)
    {
        var branch = branches[0];
        var branchTrainers = trainers.Where(t => t.BranchId == branch.Id).ToList();
        var rng = new Random(7007);

        Trainer? PickTrainer() => branchTrainers.Count == 0 ? null : branchTrainers[rng.Next(branchTrainers.Count)];

        var classTypes = new Dictionary<string, ClassType>
        {
            ["Spin"] = new() { TenantId = tenantId, Name = "Spin", Description = "High-energy indoor cycling.", DefaultDurationMinutes = 45, DefaultCapacity = 24, ColorHex = "#2563eb" },
            ["Yoga"] = new() { TenantId = tenantId, Name = "Yoga Flow", Description = "Vinyasa flow for all levels.", DefaultDurationMinutes = 60, DefaultCapacity = 20, ColorHex = "#16a34a" },
            ["HIIT"] = new() { TenantId = tenantId, Name = "HIIT", Description = "High-intensity interval training.", DefaultDurationMinutes = 30, DefaultCapacity = 16, ColorHex = "#dc2626" },
            ["Strength"] = new() { TenantId = tenantId, Name = "Strength Circuit", Description = "Full-body strength circuit.", DefaultDurationMinutes = 50, DefaultCapacity = 14, ColorHex = "#7c3aed" },
            ["Pilates"] = new() { TenantId = tenantId, Name = "Pilates", Description = "Core and mobility focused mat work.", DefaultDurationMinutes = 55, DefaultCapacity = 18, ColorHex = "#db2777" }
        };

        db.ClassTypes.AddRange(classTypes.Values);

        var slots = new (string Type, DayOfWeek Day, int Hour, int Minute, string Location)[]
        {
            ("HIIT", DayOfWeek.Monday, 6, 0, "Studio B"),
            ("Spin", DayOfWeek.Monday, 18, 0, "Studio A"),
            ("Yoga", DayOfWeek.Tuesday, 7, 0, "Studio B"),
            ("Strength", DayOfWeek.Tuesday, 19, 0, "Main Floor"),
            ("Spin", DayOfWeek.Wednesday, 6, 0, "Studio A"),
            ("Pilates", DayOfWeek.Wednesday, 18, 0, "Studio B"),
            ("HIIT", DayOfWeek.Thursday, 7, 0, "Studio B"),
            ("Yoga", DayOfWeek.Friday, 17, 30, "Studio B"),
            ("Strength", DayOfWeek.Saturday, 9, 0, "Main Floor"),
            ("Spin", DayOfWeek.Saturday, 10, 30, "Studio A")
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var throughDate = today.AddDays(ClassSessionPlanner.DefaultWindowDays);

        foreach (var slot in slots)
        {
            var classType = classTypes[slot.Type];
            var trainer = PickTrainer();

            var schedule = new ClassSchedule
            {
                TenantId = tenantId,
                BranchId = branch.Id,
                ClassType = classType,
                TrainerId = trainer?.Id,
                DayOfWeek = slot.Day,
                StartTime = new TimeOnly(slot.Hour, slot.Minute),
                DurationMinutes = classType.DefaultDurationMinutes,
                Capacity = classType.DefaultCapacity,
                Location = slot.Location,
                IsActive = true,
                GeneratedThroughDate = throughDate
            };

            db.ClassSchedules.Add(schedule);

            // ClassTypeId is only set once EF resolves the ClassType nav on save, so stamp it here
            // for the pre-generated sessions (which the planner copies from the schedule).
            schedule.ClassTypeId = classType.Id;
            var sessions = ClassSessionPlanner.BuildSessions(schedule, today, throughDate, new HashSet<DateOnly>());
            db.ClassSessions.AddRange(sessions);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
