using GymOS.Domain.Attendance;
using GymOS.Domain.Experience;
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
    /// Three exercise-progression catalogs (blueprint's SkillTree/SkillNode, Phase 7) built entirely
    /// from the exercise library above — never new exercises, since a skill tree only orders existing
    /// ones into a "next step" path. Each node's MinReps is the best single-set rep count that
    /// "unlocks" it; the demo member's seeded history (SeedDemoMemberIntelligenceDataAsync) clears the
    /// middle node of the Push and Leg trees, so the portal has real ExerciseSubstitution
    /// recommendations to show (Overhead Press after Bench Press, Deadlift after Barbell Squat) without
    /// this method needing to know about that data itself — it just needs the exercises to exist.
    /// </summary>
    private async Task SeedSkillTreesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var trees = new (string TreeName, string? MuscleGroup, (string Exercise, int MinReps, string Explanation)[] Nodes)[]
        {
            ("Push Strength Progression", "Chest",
            [
                ("Push-Up", 15, "The foundation bodyweight press — master control of your own bodyweight before loading a bar."),
                ("Bench Press", 6, "Once push-ups are easy, Bench Press adds external load to keep building horizontal pressing strength."),
                ("Overhead Press", 6, "You've built solid horizontal pressing strength on Bench Press — Overhead Press extends that to vertical pressing and shoulder stability."),
            ]),
            ("Pull Strength Progression", "Back",
            [
                ("Lat Pulldown", 10, "A machine-assisted pulling movement — builds the lat strength a full pull-up demands."),
                ("Bent-Over Row", 8, "Adds a free-weight hinge to your pulling strength, closing the gap toward a strict pull-up."),
                ("Pull-Up", 5, "The full bodyweight test of everything Lat Pulldown and Bent-Over Row have been building toward."),
            ]),
            ("Leg Strength Progression", "Legs",
            [
                ("Leg Press", 10, "A machine-supported leg press builds the base quad/glute strength a free-weight squat demands."),
                ("Barbell Squat", 6, "Once Leg Press feels light, Barbell Squat adds the stability and core demand of a free-weight lift."),
                ("Deadlift", 5, "You've mastered the squat pattern — Deadlift builds the same leg drive with a stronger posterior-chain and hip-hinge emphasis."),
            ]),
        };

        foreach (var (treeName, muscleGroup, nodes) in trees)
        {
            var tree = new SkillTree { TenantId = tenantId, Name = treeName, MuscleGroup = muscleGroup };
            db.SkillTrees.Add(tree);

            for (var i = 0; i < nodes.Length; i++)
            {
                var (exerciseName, minReps, explanation) = nodes[i];
                var exercise = await db.Exercises.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Name == exerciseName, cancellationToken);
                if (exercise is null)
                {
                    continue; // exercise library changed and no longer has this name — skip rather than fail seeding.
                }

                tree.Nodes.Add(new SkillNode { ExerciseId = exercise.Id, OrderIndex = i, MinReps = minReps, UnlockExplanation = explanation });
            }
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

        // Four consecutive weeks of habit activity so the portal's weekly-streak card reads as a
        // meaningful 4-week streak on all three tracks (check-ins, workouts, nutrition). Subtracting
        // exactly 7*w days keeps the same weekday, so each entry lands cleanly inside its week bucket
        // (Monday-start) regardless of what day the demo is seeded — no week-boundary flakiness.
        var streakFood = await db.FoodItems.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TenantId == member.TenantId && f.Name == "Greek Yogurt (plain)", cancellationToken);
        for (var w = 0; w < 4; w++)
        {
            var at = now.AddDays(-7 * w);
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                TenantId = member.TenantId, BranchId = member.BranchId, MemberId = member.Id,
                CheckInAt = at, CheckOutAt = at.AddHours(1), Method = AttendanceMethod.QrSimulated
            });
            db.WorkoutLogs.Add(new WorkoutLog { MemberId = member.Id, LoggedAt = at });
            if (streakFood is not null)
            {
                dietPlan.MealEntries.Add(new MealEntry { FoodItemId = streakFood.Id, MealType = MealType.Snack, Quantity = 1m, ConsumedAt = at });
            }
        }

        // A couple of logged recovery days so the portal's recovery card shows rest in the training mix
        // (the member can still log today's from the UI to earn recovery XP live). Seeded directly, so —
        // like the workout logs above — no RaiseLogged()/event fires and no XP is granted at seed time.
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        db.RecoveryLogs.Add(new RecoveryLog { MemberId = member.Id, LoggedOn = today.AddDays(-3), Kind = RecoveryKind.RestDay, Notes = "Full rest day" });
        db.RecoveryLogs.Add(new RecoveryLog { MemberId = member.Id, LoggedOn = today.AddDays(-5), Kind = RecoveryKind.ActiveRecovery, Notes = "Light mobility + walk" });

        // Member Experience Engine (mastery + personal records): mirror the two lifts seeded above so
        // the demo member's mastery and PR cards are populated. Seeded directly (values match what the
        // event-driven WorkoutProgressionService would compute) because seeding runs outside the event
        // path — the WorkoutLogs above don't call RaiseLogged().
        void SeedExerciseProgression(
            Guid exerciseId, int sessions, int totalSets, long totalReps, decimal totalVolume,
            decimal bestWeight, int bestReps, decimal bestSessionVolume, DateTimeOffset lastTrained, DateTimeOffset prAchievedAt)
        {
            db.ExerciseMasteries.Add(new ExerciseMastery
            {
                TenantId = member.TenantId,
                MemberId = member.Id,
                ExerciseId = exerciseId,
                Sessions = sessions,
                TotalSets = totalSets,
                TotalReps = totalReps,
                TotalVolume = totalVolume,
                BestWeightKg = bestWeight,
                BestEstimatedOneRepMax = OneRepMax.Epley(bestWeight, bestReps),
                LastTrainedAt = lastTrained,
                UpdatedAt = now
            });

            var records = new (PersonalRecordType Type, decimal Value)[]
            {
                (PersonalRecordType.MaxWeight, bestWeight),
                (PersonalRecordType.EstimatedOneRepMax, OneRepMax.Epley(bestWeight, bestReps)),
                (PersonalRecordType.SessionVolume, bestSessionVolume)
            };

            foreach (var (type, value) in records)
            {
                db.PersonalRecords.Add(new PersonalRecord
                {
                    TenantId = member.TenantId,
                    MemberId = member.Id,
                    ExerciseId = exerciseId,
                    Type = type,
                    Value = value,
                    AchievedAt = prAchievedAt
                });
            }
        }

        if (benchPress is not null)
        {
            // 2 sessions of 3x8 @ 60kg: volume 2880, best-session volume 1440, best 60kg.
            SeedExerciseProgression(benchPress.Id, 2, 6, 48, 2880m, 60m, 8, 1440m, now.AddDays(-1), now.AddDays(-7));
        }

        if (squat is not null)
        {
            // 4x6 @ 80kg then @ 85kg: volume 3960, best-session volume 2040, best 85kg (set most recently).
            SeedExerciseProgression(squat.Id, 2, 8, 48, 3960m, 85m, 6, 2040m, now.AddDays(-2), now.AddDays(-2));
        }

        // Member Experience Engine: give the linked demo member a populated Level/XP card out of the
        // box. Awards are written directly here because the seeder runs outside an HTTP context, so
        // the event-driven XP path that fires on live check-ins/workouts never runs during seeding —
        // the WorkoutLogs added above deliberately don't call RaiseLogged(). Four workouts (50 XP
        // each) + ten visits (20 XP each) = 400 XP, which lands the member at level 3.
        var xpSeeds = new List<(XpReason Reason, XpSourceType Source, int DaysAgo)>();
        for (var d = 1; d <= 4; d++)
        {
            xpSeeds.Add((XpReason.WorkoutCompleted, XpSourceType.WorkoutLog, d * 2));
        }

        for (var d = 1; d <= 10; d++)
        {
            xpSeeds.Add((XpReason.GymVisit, XpSourceType.Attendance, d));
        }

        long totalXp = 0;
        foreach (var (reason, source, daysAgo) in xpSeeds)
        {
            var amount = XpPolicy.AwardFor(reason);
            totalXp += amount;
            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = member.TenantId,
                MemberId = member.Id,
                Amount = amount,
                Reason = reason,
                SourceType = source,
                SourceId = Guid.NewGuid(),
                OccurredAt = now.AddDays(-daysAgo)
            });
        }

        var progression = new MemberProgression { TenantId = member.TenantId, MemberId = member.Id };
        progression.SetTotalXp(totalXp);
        progression.UpdatedAt = now;
        db.MemberProgressions.Add(progression);

        // Achievements the seeded activity clearly earns (first workout/visit, reached level 3, set a
        // PR). Seeded directly since SetTotalXp above is silent — it doesn't raise the progression
        // event that would otherwise trigger live achievement evaluation.
        var earnedAchievements = new (string Code, int DaysAgo)[]
        {
            ("first-visit", 30),
            ("first-workout", 10),
            ("first-pr", 7),
            ("level-3", 1)
        };

        foreach (var (code, daysAgo) in earnedAchievements)
        {
            db.MemberAchievements.Add(new MemberAchievement
            {
                TenantId = member.TenantId,
                MemberId = member.Id,
                Code = code,
                UnlockedAt = now.AddDays(-daysAgo)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
