using GymOS.Domain.Identity;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Workouts;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Infrastructure.Seeding;

public partial class DemoDataSeeder
{
    private async Task SeedFoodLibraryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var foods = new (string Name, decimal Calories, decimal Protein, decimal Carbs, decimal Fat, string Serving)[]
        {
            ("Chicken Breast (grilled)", 165, 31, 0, 3.6m, "100g"),
            ("Brown Rice (cooked)", 216, 5, 45, 1.8m, "1 cup"),
            ("Broccoli (steamed)", 55, 3.7m, 11, 0.6m, "1 cup"),
            ("Whey Protein Shake", 120, 24, 3, 1.5m, "1 scoop"),
            ("Greek Yogurt (plain)", 100, 17, 6, 0.7m, "170g"),
            ("Oatmeal", 150, 5, 27, 3, "1 cup cooked"),
            ("Almonds", 164, 6, 6, 14, "28g"),
            ("Banana", 105, 1.3m, 27, 0.4m, "1 medium"),
            ("Salmon (baked)", 206, 22, 0, 13, "100g"),
            ("Sweet Potato (baked)", 103, 2, 24, 0.2m, "1 medium"),
            ("Egg (whole)", 78, 6.3m, 0.6m, 5.3m, "1 large"),
            ("Avocado", 234, 2.9m, 12, 21, "1 medium"),
        };

        foreach (var (name, calories, protein, carbs, fat, serving) in foods)
        {
            db.FoodItems.Add(new FoodItem
            {
                TenantId = tenantId,
                Name = name,
                CaloriesPerServing = calories,
                ProteinG = protein,
                CarbsG = carbs,
                FatG = fat,
                ServingSizeDescription = serving
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedExerciseLibraryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exercises = new (string Name, string MuscleGroup, string Equipment)[]
        {
            ("Barbell Squat", "Legs", "Barbell"),
            ("Bench Press", "Chest", "Barbell"),
            ("Deadlift", "Back", "Barbell"),
            ("Overhead Press", "Shoulders", "Barbell"),
            ("Bent-Over Row", "Back", "Barbell"),
            ("Pull-Up", "Back", "Bodyweight"),
            ("Push-Up", "Chest", "Bodyweight"),
            ("Dumbbell Curl", "Arms", "Dumbbell"),
            ("Tricep Pushdown", "Arms", "Cable Machine"),
            ("Lat Pulldown", "Back", "Cable Machine"),
            ("Leg Press", "Legs", "Machine"),
            ("Leg Curl", "Legs", "Machine"),
            ("Plank", "Core", "Bodyweight"),
            ("Treadmill Run", "Cardio", "Treadmill"),
            ("Rowing Machine", "Full Body", "Rowing Machine"),
        };

        foreach (var (name, muscleGroup, equipment) in exercises)
        {
            db.Exercises.Add(new Exercise { TenantId = tenantId, Name = name, MuscleGroup = muscleGroup, Equipment = equipment });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gives the linked demo member login (see LinkDemoMemberAccountAsync) real workout and
    /// nutrition history to show — a plateaued lift (identical weight/reps two sessions running, so
    /// the portal's progressive-overload card has a real "add weight next time" to display) and a
    /// second lift trending up (so "progressing" shows too), plus a diet plan with today's meals
    /// actually logged against it. Must run after SeedExerciseLibraryAsync/SeedFoodLibraryAsync —
    /// it looks up exercises/food items by name rather than holding references from those steps, so
    /// call order between the three only matters in that one direction.
    /// </summary>
    private async Task SeedDemoMemberIntelligenceDataAsync(Dictionary<string, User> demoUsers, CancellationToken cancellationToken)
    {
        var member = await db.Members.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == demoUsers[RoleNames.Member].Id, cancellationToken);
        if (member is null)
        {
            return; // LinkDemoMemberAccountAsync found no eligible candidate to link — nothing to attach this to.
        }

        var now = DateTimeOffset.UtcNow;

        var benchPress = await db.Exercises.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == member.TenantId && e.Name == "Bench Press", cancellationToken);
        if (benchPress is not null)
        {
            // Same weight, same reps two sessions running -> ProgressiveOverloadPolicy reads this as
            // a plateau and the portal suggests adding weight next time.
            var older = new WorkoutLog { MemberId = member.Id, LoggedAt = now.AddDays(-7) };
            older.Entries.Add(new WorkoutLogEntry { ExerciseId = benchPress.Id, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            db.WorkoutLogs.Add(older);

            var newer = new WorkoutLog { MemberId = member.Id, LoggedAt = now.AddDays(-1) };
            newer.Entries.Add(new WorkoutLogEntry { ExerciseId = benchPress.Id, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            db.WorkoutLogs.Add(newer);
        }

        var squat = await db.Exercises.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == member.TenantId && e.Name == "Barbell Squat", cancellationToken);
        if (squat is not null)
        {
            // Heavier than last time -> reads as already progressing, no nudge needed.
            var older = new WorkoutLog { MemberId = member.Id, LoggedAt = now.AddDays(-10) };
            older.Entries.Add(new WorkoutLogEntry { ExerciseId = squat.Id, SetsCompleted = 4, RepsCompleted = 6, WeightKg = 80m });
            db.WorkoutLogs.Add(older);

            var newer = new WorkoutLog { MemberId = member.Id, LoggedAt = now.AddDays(-2) };
            newer.Entries.Add(new WorkoutLogEntry { ExerciseId = squat.Id, SetsCompleted = 4, RepsCompleted = 6, WeightKg = 85m });
            db.WorkoutLogs.Add(newer);
        }

        var dietPlan = new DietPlan
        {
            MemberId = member.Id,
            Name = "Lean Muscle Plan",
            TargetCalories = 2400m,
            TargetProteinG = 180m,
            TargetCarbsG = 250m,
            TargetFatG = 70m,
            StartDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-30)
        };
        db.DietPlans.Add(dietPlan);

        // Small, clustered hour offsets — large ones (e.g. "8 hours ago") risk crossing the UTC
        // midnight boundary depending what time of day the seed happens to run, which would silently
        // drop the entry from "today's" totals the portal shows. Demo data must be robust to when
        // the demo is actually run, not just to when it happened to be seeded.
        var mealFoods = new[] { ("Chicken Breast (grilled)", MealType.Lunch, 1.5m, -1), ("Brown Rice (cooked)", MealType.Lunch, 1m, -1), ("Whey Protein Shake", MealType.Breakfast, 1m, -2) };
        foreach (var (foodName, mealType, quantity, hoursAgo) in mealFoods)
        {
            var food = await db.FoodItems.IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.TenantId == member.TenantId && f.Name == foodName, cancellationToken);
            if (food is not null)
            {
                dietPlan.MealEntries.Add(new MealEntry { FoodItemId = food.Id, MealType = mealType, Quantity = quantity, ConsumedAt = now.AddHours(hoursAgo) });
            }
        }

        db.WaterLogs.Add(new WaterLog { MemberId = member.Id, AmountMl = 750, LoggedAt = now.AddHours(-3) });

        await db.SaveChangesAsync(cancellationToken);
    }
}
