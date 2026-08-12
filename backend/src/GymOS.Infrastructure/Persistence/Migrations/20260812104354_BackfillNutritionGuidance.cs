using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Gives the already-seeded diet plans something to say, so the deployed instance can actually show
/// the feature that was just built for it.
///
/// The member's nutrition screen became a prescription: standing instructions, what changes this
/// week, what changes this month. DemoDataSeeder now writes all of that — and only ever runs against
/// an EMPTY database, so the deployed instance, which has had a tenant since it first came up, would
/// have shown a plan name, four targets and then nothing at all underneath. Functional, honest, and
/// completely uninformative about the thing it exists to demonstrate. Exactly the shape of the
/// treadmill defect that AddExerciseLoadType left behind and BackfillExerciseLoadTypes had to clean
/// up: the seeder knew, the deployment could not hear it.
///
/// MATCHING IS DELIBERATELY NARROW, and narrower than usual because of what is being written. The
/// previous backfill CORRECTED wrong data; this one ADDS words attributed to a nutritionist. Putting
/// sentences in a real professional's mouth would be a serious thing to do, so every statement below
/// is scoped by exact plan name to the eight plans DemoDataSeeder itself creates. A gym that has
/// typed its own plans matches nothing here and is left completely untouched.
///
/// Notes are only written where the column is NULL, so a plan somebody has since edited by hand
/// keeps what they wrote. Guidance is only inserted where that plan has none, so re-running is a
/// no-op. Dates are relative to CURRENT_DATE for the same reason every seeded date is: a demo run
/// in three months should still have a "this week", not a note that expired before anyone logged in.
///
/// Down() removes only the rows this migration could have written — guidance for those plan names,
/// and the notes it set — and cannot distinguish a note it wrote from one edited afterwards, which
/// is why it clears only exact matches of the strings below.
/// </summary>
public partial class BackfillNutritionGuidance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---- Standing instructions, one per seeded plan ----
        migrationBuilder.Sql("""
            UPDATE "DietPlans" p
               SET "Notes" = v.notes
              FROM (VALUES
                ('Lean Muscle Plan',
                 'Protein at every meal, starting with breakfast. Water before coffee. Weigh in Monday mornings before eating.'),
                ('Cutting Phase — 12 week',
                 'Two litres of water a day minimum. Keep protein fixed even on low days — the deficit comes out of carbs and fat.'),
                ('High Protein Rebuild',
                 'Aim for a palm of protein at four points in the day rather than two large hits.'),
                ('Marathon Fuelling',
                 'Carbs go UP the day before a long run and stay up the morning after. Do not train the day-before carbs down.'),
                ('Post-Injury Recovery Plan',
                 'Protein and sleep are doing the work here. Nothing aggressive until the physio signs it off.'),
                ('Weight Loss — Phase 1',
                 'One change at a time. This phase is only about the evening meal and what happens after it.'),
                ('Vegetarian Macro Split',
                 'Combine sources across the day rather than per meal. Iron with something acidic, never with tea.'),
                ('Pre-Competition Peak',
                 'Nothing new in the last ten days. Everything you eat this fortnight you have eaten before.')
              ) AS v(name, notes)
             WHERE p."Name" = v.name
               AND p."Notes" IS NULL;
            """);

        // ---- What changes this week and this month ----
        //
        // Two weeklies per plan on purpose: one current and one already superseded, so the screen's
        // "what has changed" history has something in it. That history is the part that makes a plan
        // read as progressing rather than as a page that has always said the same thing.
        migrationBuilder.Sql("""
            INSERT INTO "DietPlanGuidance"
                   ("Id", "TenantId", "DietPlanId", "Cadence", "EffectiveFrom", "Title", "Body", "CreatedByUserId", "CreatedAt")
            SELECT gen_random_uuid(), p."TenantId", p."Id", v.cadence,
                   CURRENT_DATE - v.days_ago, v.title, v.body, p."CreatedByUserId",
                   NOW() - (v.days_ago || ' days')::interval
              FROM "DietPlans" p
              JOIN (VALUES
                ('Lean Muscle Plan', 'Monthly', 12, 'Lean gain block, week 2 of 4',
                 'Calories stay where they are. We are looking for 0.25-0.5 kg a week on the scale, no more.'),
                ('Lean Muscle Plan', 'Weekly', 2, 'Carbs down on rest days',
                 'On days you do not train, drop about 60g of carbs and keep protein identical.'),
                ('Lean Muscle Plan', 'Weekly', 9, 'Front-load protein', NULL),

                ('Cutting Phase — 12 week', 'Monthly', 14, 'Week 6 of 12 — halfway',
                 'The rate has been right. No change to the targets this month.'),
                ('Cutting Phase — 12 week', 'Weekly', 3, 'Add a refeed on Saturday', NULL),
                ('Cutting Phase — 12 week', 'Weekly', 10, 'Hold everything, no changes', NULL),

                ('High Protein Rebuild', 'Monthly', 10, 'Rebuild block, month 1',
                 'Protein up, everything else steady. Strength on the bar is the metric, not the scale.'),
                ('High Protein Rebuild', 'Weekly', 1, 'A shake straight after training', NULL),

                ('Marathon Fuelling', 'Monthly', 20, 'Race build, four weeks out',
                 'Practise race-day breakfast on every long run from here.'),
                ('Marathon Fuelling', 'Weekly', 4, 'Carb load before Sunday long runs', NULL),

                ('Post-Injury Recovery Plan', 'Monthly', 18, 'Recovery block',
                 'Nothing changes until the physio review. Eat to repair, not to a target.'),

                ('Weight Loss — Phase 1', 'Monthly', 25, 'Phase 1, month 1',
                 'One habit at a time. We are not counting anything yet.'),
                ('Weight Loss — Phase 1', 'Weekly', 5, 'Evening meal only this week', NULL),

                ('Vegetarian Macro Split', 'Monthly', 8, 'Establishing the split',
                 'Getting the protein number reliably is the whole job this month.'),
                ('Vegetarian Macro Split', 'Weekly', 2, 'Lentils or tofu at lunch', NULL),

                ('Pre-Competition Peak', 'Weekly', 3, 'Nothing new from here', NULL)
              ) AS v(name, cadence, days_ago, title, body) ON p."Name" = v.name
             WHERE NOT EXISTS (
                   SELECT 1 FROM "DietPlanGuidance" g WHERE g."DietPlanId" = p."Id");
            """);

        // ---- A little adherence history, so the confirmation has a real denominator ----
        //
        // Deliberately never TODAY. The member should meet an unpressed button — a screen that opens
        // already ticked demonstrates nothing, and the whole point of the control is the tap.
        migrationBuilder.Sql("""
            INSERT INTO "PlanAdherenceLogs" ("Id", "TenantId", "MemberId", "DietPlanId", "OnDate", "Note", "LoggedAt")
            SELECT gen_random_uuid(), p."TenantId", p."MemberId", p."Id",
                   CURRENT_DATE - d.days_ago, NULL, NOW() - (d.days_ago || ' days')::interval
              FROM "DietPlans" p
             CROSS JOIN (VALUES (1), (2), (4), (5), (8), (9), (11)) AS d(days_ago)
             WHERE p."Name" IN (
                     'Lean Muscle Plan', 'Cutting Phase — 12 week', 'High Protein Rebuild',
                     'Vegetarian Macro Split', 'Pre-Competition Peak')
               AND (p."EndDate" IS NULL OR p."EndDate" >= CURRENT_DATE)
               AND NOT EXISTS (
                     SELECT 1 FROM "PlanAdherenceLogs" a
                      WHERE a."MemberId" = p."MemberId" AND a."OnDate" = CURRENT_DATE - d.days_ago);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "PlanAdherenceLogs" a
             USING "DietPlans" p
             WHERE a."DietPlanId" = p."Id"
               AND p."Name" IN (
                     'Lean Muscle Plan', 'Cutting Phase — 12 week', 'High Protein Rebuild',
                     'Vegetarian Macro Split', 'Pre-Competition Peak');

            DELETE FROM "DietPlanGuidance" g
             USING "DietPlans" p
             WHERE g."DietPlanId" = p."Id"
               AND p."Name" IN (
                     'Lean Muscle Plan', 'Cutting Phase — 12 week', 'High Protein Rebuild',
                     'Marathon Fuelling', 'Post-Injury Recovery Plan', 'Weight Loss — Phase 1',
                     'Vegetarian Macro Split', 'Pre-Competition Peak');

            UPDATE "DietPlans" SET "Notes" = NULL
             WHERE "Name" IN (
                     'Lean Muscle Plan', 'Cutting Phase — 12 week', 'High Protein Rebuild',
                     'Marathon Fuelling', 'Post-Injury Recovery Plan', 'Weight Loss — Phase 1',
                     'Vegetarian Macro Split', 'Pre-Competition Peak');
            """);
    }
}
