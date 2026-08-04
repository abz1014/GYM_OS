using GymOS.Domain.Classes;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Infrastructure.Seeding;

public partial class DemoDataSeeder
{
    /// <summary>
    /// Seeds a realistic weekly group-class timetable for the primary branch, pre-materialises the
    /// first booking window of concrete sessions (same ClassSessionPlanner the runtime job uses),
    /// and books members into those sessions — filling some to capacity and pushing a couple onto a
    /// waitlist — so a fresh demo shows a populated calendar and realistic rosters immediately.
    /// </summary>
    private async Task SeedClassesAsync(Guid tenantId, List<Branch> branches, List<Trainer> trainers, List<Member> members, CancellationToken cancellationToken)
    {
        var branch = branches[0];
        var branchTrainers = trainers.Where(t => t.BranchId == branch.Id).ToList();
        var rng = new Random(7007);
        var allSessions = new List<ClassSession>();

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
            allSessions.AddRange(sessions);
        }

        SeedBookings(allSessions, members.Where(m => m.BranchId == branch.Id).ToList(), tenantId, branch.Id, rng);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Fills each session to a random level: everyone up to capacity is Booked, and a session that
    /// draws more than its capacity spills the extras onto the waitlist (Booked/Waitlisted set
    /// directly here rather than via the command, but following the same capacity rule) — so the
    /// demo shows real "18/20 booked · 2 waitlisted" rosters out of the box.
    /// </summary>
    private void SeedBookings(List<ClassSession> sessions, List<Member> branchMembers, Guid tenantId, Guid branchId, Random rng)
    {
        if (branchMembers.Count == 0)
        {
            return;
        }

        foreach (var session in sessions)
        {
            // Draw 50%–120% of capacity so most classes are healthily full and some overflow.
            var demand = (int)Math.Round(session.Capacity * (0.5 + rng.NextDouble() * 0.7));
            demand = Math.Min(demand, branchMembers.Count);

            var attendees = branchMembers.OrderBy(_ => rng.Next()).Take(demand).ToList();

            for (var i = 0; i < attendees.Count; i++)
            {
                db.ClassBookings.Add(new ClassBooking
                {
                    TenantId = tenantId,
                    BranchId = branchId,
                    ClassSessionId = session.Id,
                    MemberId = attendees[i].Id,
                    Status = i < session.Capacity ? ClassBookingStatus.Booked : ClassBookingStatus.Waitlisted,
                    // Stagger so the waitlist has a deterministic FIFO order.
                    BookedAt = DateTimeOffset.UtcNow.AddMinutes(-demand + i)
                });
            }
        }
    }
}
