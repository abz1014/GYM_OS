using GymOS.Domain.Attendance;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Trainers;
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

        // Each curated session gets a matching check-in ~40 minutes earlier. A workout with no visit
        // on record is exactly the inconsistency the capture-rate sanity metric exists to flag, and
        // the one member every demo drills into shouldn't be the one tripping it.
        void AddVisitFor(DateTimeOffset loggedAt)
        {
            var checkIn = loggedAt.AddMinutes(-40);
            db.AttendanceRecords.Add(new Domain.Attendance.AttendanceRecord
            {
                TenantId = member.TenantId,
                BranchId = member.BranchId,
                MemberId = member.Id,
                CheckInAt = checkIn,
                CheckOutAt = loggedAt.AddMinutes(15),
                Method = Domain.Attendance.AttendanceMethod.QrSimulated
            });
        }

        var benchPress = await db.Exercises.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == member.TenantId && e.Name == "Bench Press", cancellationToken);
        if (benchPress is not null)
        {
            // Same weight, same reps two sessions running -> ProgressiveOverloadPolicy reads this as
            // a plateau and the portal suggests adding weight next time.
            var older = new WorkoutLog { TenantId = member.TenantId, MemberId = member.Id, LoggedAt = now.AddDays(-7) };
            AddVisitFor(older.LoggedAt);
            older.Entries.Add(new WorkoutLogEntry { TenantId = member.TenantId, ExerciseId = benchPress.Id, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            older.RaiseLogged();
            db.WorkoutLogs.Add(older);

            var newer = new WorkoutLog { TenantId = member.TenantId, MemberId = member.Id, LoggedAt = now.AddDays(-1) };
            AddVisitFor(newer.LoggedAt);
            newer.Entries.Add(new WorkoutLogEntry { TenantId = member.TenantId, ExerciseId = benchPress.Id, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            newer.RaiseLogged();
            db.WorkoutLogs.Add(newer);
        }

        var squat = await db.Exercises.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == member.TenantId && e.Name == "Barbell Squat", cancellationToken);
        if (squat is not null)
        {
            // Heavier than last time -> reads as already progressing, no nudge needed.
            var older = new WorkoutLog { TenantId = member.TenantId, MemberId = member.Id, LoggedAt = now.AddDays(-10) };
            AddVisitFor(older.LoggedAt);
            older.Entries.Add(new WorkoutLogEntry { TenantId = member.TenantId, ExerciseId = squat.Id, SetsCompleted = 4, RepsCompleted = 6, WeightKg = 80m });
            older.RaiseLogged();
            db.WorkoutLogs.Add(older);

            var newer = new WorkoutLog { TenantId = member.TenantId, MemberId = member.Id, LoggedAt = now.AddDays(-2) };
            AddVisitFor(newer.LoggedAt);
            newer.Entries.Add(new WorkoutLogEntry { TenantId = member.TenantId, ExerciseId = squat.Id, SetsCompleted = 4, RepsCompleted = 6, WeightKg = 85m });
            newer.RaiseLogged();
            db.WorkoutLogs.Add(newer);
        }

        var dietPlan = new DietPlan
        {
            TenantId = member.TenantId,
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
                dietPlan.MealEntries.Add(new MealEntry { TenantId = member.TenantId, FoodItemId = food.Id, MealType = mealType, Quantity = quantity, ConsumedAt = now.AddHours(hoursAgo) });
            }
        }

        db.WaterLogs.Add(new WaterLog { TenantId = member.TenantId, MemberId = member.Id, AmountMl = 750, LoggedAt = now.AddHours(-3) });

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
            db.WorkoutLogs.Add(new WorkoutLog { TenantId = member.TenantId, MemberId = member.Id, LoggedAt = at });
            if (streakFood is not null)
            {
                dietPlan.MealEntries.Add(new MealEntry { TenantId = member.TenantId, FoodItemId = streakFood.Id, MealType = MealType.Snack, Quantity = 1m, ConsumedAt = at });
            }
        }

        // A couple of logged recovery days so the portal's recovery card shows rest in the training mix
        // (the member can still log today's from the UI to earn recovery XP live). Seeded directly, so —
        // like the workout logs above — no RaiseLogged()/event fires and no XP is granted at seed time.
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        db.RecoveryLogs.Add(new RecoveryLog { TenantId = member.TenantId, MemberId = member.Id, LoggedOn = today.AddDays(-3), Kind = RecoveryKind.RestDay, Notes = "Full rest day" });
        db.RecoveryLogs.Add(new RecoveryLog { TenantId = member.TenantId, MemberId = member.Id, LoggedOn = today.AddDays(-5), Kind = RecoveryKind.ActiveRecovery, Notes = "Light mobility + walk" });

        // The Experience Engine now owns everything that used to be hand-written here — mastery,
        // personal records, XP, level and achievements. Those blocks existed because seeding ran
        // outside the event path, so nothing computed them; that premise is gone. SeedMemberActivityAsync
        // gives this member (and everyone else) real logged history, and the WorkoutLogs above now call
        // RaiseLogged(), so the same handlers that run for a live workout produce all of it.
        //
        // Removing them is not just de-duplication. Hand-written projections silently drift from the
        // rules the moment either side changes, and they were already colliding with the computed rows
        // on the one-row-per-member-per-exercise index. What the demo shows is now what the engine
        // actually calculated.

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Two community challenges so the portal has a real example of each state: one the demo member
    /// hasn't joined yet (a live "join" click completes it immediately, since their recent workout
    /// history already clears its target — the intended, convincing demo path), and one they're
    /// already partway through (seeded as an in-progress participant, not yet at its higher target).
    /// </summary>
    private async Task SeedCommunityChallengesAsync(Dictionary<string, User> demoUsers, CancellationToken cancellationToken)
    {
        var member = await db.Members.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == demoUsers[RoleNames.Member].Id, cancellationToken);
        if (member is null)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        // Tenant-wide, easy target — the demo member's last week already clears it, so joining live
        // completes it immediately and grants challenge XP + the "Challenge Accepted" achievement.
        var quickChallenge = new CommunityChallenge
        {
            TenantId = member.TenantId,
            BranchId = null,
            Name = "August Consistency Challenge",
            Description = "Log 3 workouts this week to earn the badge.",
            StartDate = today.AddDays(-6),
            EndDate = today.AddDays(7),
            TargetWorkoutCount = 3,
            CreatedByUserId = demoUsers[RoleNames.Owner].Id
        };
        db.CommunityChallenges.Add(quickChallenge);

        // Branch-specific, higher target — the member is already in it but hasn't hit 10 yet, so the
        // portal shows a genuine "in progress" state alongside the completed one above.
        var enduranceChallenge = new CommunityChallenge
        {
            TenantId = member.TenantId,
            BranchId = member.BranchId,
            Name = "Titan Iron Challenge",
            Description = "10 workouts in two weeks — for the truly committed.",
            StartDate = today.AddDays(-13),
            EndDate = today,
            TargetWorkoutCount = 10,
            CreatedByUserId = demoUsers[RoleNames.Owner].Id
        };
        db.CommunityChallenges.Add(enduranceChallenge);

        await db.SaveChangesAsync(cancellationToken);

        db.ChallengeParticipants.Add(new ChallengeParticipant
        {
            ChallengeId = enduranceChallenge.Id,
            MemberId = member.Id,
            JoinedAt = DateTimeOffset.UtcNow.AddDays(-12),
            IsCompleted = false
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The trainer-written side of training: a library of programmes, and the members who are on one.
    ///
    /// Nothing here is new capability — templates, template exercises and assignments have been built,
    /// tested and reachable from the staff UI since Wave 3, and the member portal's top logging tier
    /// ("confirm your trainer's plan") reads them. None of it had ever held a row, so the whole chain
    /// was invisible: the Assigned Workouts card was permanently empty, the plan tier never fired, and
    /// the two ordering defects found validating Step 1 could not be seen working.
    ///
    /// Programmes are built only from the existing exercise library, the same discipline the skill
    /// trees follow — a plan invents no movement the gym does not own.
    ///
    /// Only members who actually have a trainer get one. That is the honest rule and it matches the
    /// research behind Step 1's tiering: 12.5–29% of gym members work with a trainer, which is exactly
    /// why "repeat last session" is the majority path and the plan tier is the enhancement. Seeding
    /// plans onto everyone would invert the product's own thesis in the demo.
    /// </summary>
    private async Task SeedWorkoutPlansAsync(Guid tenantId, List<Trainer> trainers, CancellationToken cancellationToken)
    {
        var programmes = new (string Name, string Description, (string Exercise, int Sets, int Reps)[] Exercises)[]
        {
            ("Beginner Full Body", "Three sessions a week, whole body each time — the starting point for a new member.",
            [
                ("Barbell Squat", 3, 8), ("Bench Press", 3, 8), ("Bent-Over Row", 3, 10), ("Plank", 3, 30),
            ]),
            ("Push Day", "Chest, shoulders and triceps. Part of a push/pull/legs split.",
            [
                ("Bench Press", 4, 6), ("Overhead Press", 3, 8), ("Tricep Pushdown", 3, 12), ("Push-Up", 3, 15),
            ]),
            ("Pull Day", "Back and biceps. Part of a push/pull/legs split.",
            [
                ("Deadlift", 4, 5), ("Pull-Up", 3, 8), ("Lat Pulldown", 3, 10), ("Dumbbell Curl", 3, 12),
            ]),
            ("Leg Day", "Quads, hamstrings and glutes. Part of a push/pull/legs split.",
            [
                ("Barbell Squat", 4, 6), ("Leg Press", 3, 10), ("Leg Curl", 3, 12),
            ]),
            ("Upper Body Strength", "Low reps, heavier loads — for a member past their first year.",
            [
                ("Bench Press", 5, 5), ("Bent-Over Row", 5, 5), ("Overhead Press", 3, 6), ("Pull-Up", 3, 6),
            ]),
            ("Conditioning & Core", "Cardio and midsection, for the days between lifting sessions.",
            [
                ("Rowing Machine", 3, 20), ("Treadmill Run", 3, 10), ("Plank", 3, 30),
            ]),
        };

        var exercisesByName = (await db.Exercises.IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId)
                .Select(e => new { e.Id, e.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.Name, e => e.Id);

        // Written by whoever runs the floor. The head trainer is as good an author as any, and it
        // makes CreatedByUserId a real person rather than a null the UI has to explain.
        var authorUserId = trainers.FirstOrDefault()?.UserId;

        var templates = new List<WorkoutTemplate>();
        foreach (var (name, description, exercises) in programmes)
        {
            var template = new WorkoutTemplate
            {
                TenantId = tenantId,
                Name = name,
                Description = description,
                CreatedByUserId = authorUserId
            };

            for (var i = 0; i < exercises.Length; i++)
            {
                var (exerciseName, sets, reps) = exercises[i];
                if (!exercisesByName.TryGetValue(exerciseName, out var exerciseId))
                {
                    continue; // the exercise library changed — skip the movement rather than fail seeding.
                }

                template.TemplateExercises.Add(new WorkoutTemplateExercise
                {
                    ExerciseId = exerciseId, SetsCount = sets, RepsCount = reps, OrderIndex = i
                });
            }

            db.WorkoutTemplates.Add(template);
            templates.Add(template);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Who is coached, and by whom — plans follow the existing pairings so a member's programme is
        // written by the trainer who actually works with them.
        var pairings = await db.TrainerAssignments.IgnoreQueryFilters()
            .Where(a => a.IsActive)
            .Select(a => new { a.MemberId, a.TrainerId })
            .ToListAsync(cancellationToken);

        var trainerUserById = trainers.ToDictionary(t => t.Id, t => t.UserId);
        var rng = new Random(9014);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var pairing in pairings.GroupBy(p => p.MemberId).Select(g => g.First()))
        {
            // Not every coached member is on a written programme — plenty are coached session by
            // session. Two in five, which is the number that matters: this gym seeds more members with
            // a trainer than the research band above describes, so gating on the pairing alone would
            // put a third of the whole gym on a plan. Two in five of the coached lands the gym-wide
            // share near 20%, inside the band, and keeps "repeat last session" plainly the majority.
            if (rng.Next(5) >= 2)
            {
                continue;
            }

            var assignedBy = trainerUserById.GetValueOrDefault(pairing.TrainerId);

            // A block that has already run its course, so the card shows history rather than only the
            // current programme — and so the "most recently started wins" rule has something to sort.
            if (rng.Next(2) == 0)
            {
                var previousStart = today.AddDays(-rng.Next(150, 260));
                db.WorkoutAssignments.Add(new WorkoutAssignment
                {
                    TenantId = tenantId,
                    MemberId = pairing.MemberId,
                    WorkoutTemplateId = templates[rng.Next(templates.Count)].Id,
                    AssignedByUserId = assignedBy,
                    StartDate = previousStart,
                    EndDate = previousStart.AddDays(84)
                });
            }

            db.WorkoutAssignments.Add(new WorkoutAssignment
            {
                TenantId = tenantId,
                MemberId = pairing.MemberId,
                WorkoutTemplateId = templates[rng.Next(templates.Count)].Id,
                AssignedByUserId = assignedBy,
                StartDate = today.AddDays(-rng.Next(5, 60)),
                EndDate = null
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await PinDemoLoginsToTheirTiersAsync(templates, trainers, today, cancellationToken);
    }

    /// <summary>
    /// Both demo member logins land on a known logging tier, rather than on whatever the assignment
    /// RNG happened to give the member each was linked to.
    ///
    /// member@ must have no ACTIVE plan, so its home screen proposes the member's own last session —
    /// the path most members get. It keeps a finished block so the Assigned Workouts card still shows
    /// real history. member2@ must have one, so its home screen proposes the trainer's programme.
    /// Between them the two tiers can be shown side by side in a single sitting.
    /// </summary>
    private async Task PinDemoLoginsToTheirTiersAsync(
        List<WorkoutTemplate> templates, List<Trainer> trainers, DateOnly today, CancellationToken cancellationToken)
    {
        if (templates.Count == 0)
        {
            return;
        }

        var selfDirected = await db.Members.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId != null && m.User!.Email == "member@titanfitness.demo", cancellationToken);
        var coached = await db.Members.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId != null && m.User!.Email == "member2@titanfitness.demo", cancellationToken);

        var headTrainer = trainers.FirstOrDefault();

        if (selfDirected is not null)
        {
            var stillRunning = await db.WorkoutAssignments.IgnoreQueryFilters()
                .Where(a => a.MemberId == selfDirected.Id && (a.EndDate == null || a.EndDate >= today))
                .ToListAsync(cancellationToken);

            // Close them off rather than delete: a plan that ran and finished is a truer history than
            // one that never existed, and it leaves the card something to show. Backdated to a full
            // twelve-week block — the assignment RNG may have started it days ago, and a programme
            // that apparently ran for under a week reads like a data error rather than a training
            // block someone finished.
            var closedOn = today.AddDays(-14);
            foreach (var assignment in stillRunning)
            {
                assignment.EndDate = closedOn;
                assignment.StartDate = closedOn.AddDays(-84);
            }

            if (stillRunning.Count == 0)
            {
                db.WorkoutAssignments.Add(new WorkoutAssignment
                {
                    TenantId = selfDirected.TenantId,
                    MemberId = selfDirected.Id,
                    WorkoutTemplateId = templates[0].Id,
                    AssignedByUserId = headTrainer?.UserId,
                    StartDate = today.AddDays(-98),
                    EndDate = today.AddDays(-14)
                });
            }
        }

        if (coached is not null)
        {
            var hasActive = await db.WorkoutAssignments.IgnoreQueryFilters()
                .AnyAsync(a => a.MemberId == coached.Id && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today), cancellationToken);

            if (!hasActive)
            {
                // "Upper Body Strength" by name rather than by index, so reordering the programme list
                // above cannot silently change which plan the demo opens on.
                var programme = templates.FirstOrDefault(t => t.Name == "Upper Body Strength") ?? templates[0];
                db.WorkoutAssignments.Add(new WorkoutAssignment
                {
                    TenantId = coached.TenantId,
                    MemberId = coached.Id,
                    WorkoutTemplateId = programme.Id,
                    AssignedByUserId = headTrainer?.UserId,
                    StartDate = today.AddDays(-21),
                    EndDate = null
                });
            }

            // A coached member needs a coach. The pairing RNG may not have given them one.
            if (headTrainer is not null
                && !await db.TrainerAssignments.IgnoreQueryFilters().AnyAsync(a => a.MemberId == coached.Id && a.IsActive, cancellationToken))
            {
                db.TrainerAssignments.Add(new TrainerAssignment
                {
                    TrainerId = headTrainer.Id,
                    MemberId = coached.Id,
                    StartDate = today.AddDays(-60),
                    IsActive = true
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
