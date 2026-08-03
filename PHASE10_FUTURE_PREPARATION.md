# Phase 10 — Future Preparation (assessment only, no implementation)

> Per GYMOS_NEXT_PHASE_EXECUTION_GUIDE.md Phase 10: confirm every module can support the
> Member Experience Engine and, later, AI — **without building either**. No AI code was written.
> The one change shipped alongside this document is a data-*preservation* fix (below), not an
> AI feature.

## The central asset: AuditLog is already an event stream

`AuditBehavior` writes one row per executed command — action name, entity type, entity id,
acting user, tenant, UTC timestamp, and the full command payload as JSON — inside the same
transaction as the business change. Verified live in this phase, e.g.:

```
RenewMembershipCommand | Members | {"MemberId":"…","MembershipPlanId":"…","StartDate":"2026-08-04",
                                   "AutoRenew":false,"CouponCode":"SUMMER20"} | 2026-08-04T…
```

That means **historical state is reconstructable for every command-driven change**, even where
the current row was overwritten in place. This is the single most valuable thing the platform
already has for future analytics, and it required no per-feature work.

**Its limitation, stated plainly:** it is JSON in a text column. It supports reconstruction and
export; it does not support efficient time-series querying ("show weight trend", "members whose
visit frequency dropped 40%"). Purpose-built projections will be needed for those — the raw
material exists, the query shape does not.

### Data-preservation defect found and fixed this phase

While reading real audit rows, `"CouponCode":"***REDACTED***"` appeared on every renewal. The
redaction rule matched the substring `code`, so three business identifiers — `CouponCode`,
`MemberCode`, `TemplateCode` — were being permanently destroyed in the audit trail. Promotion
attribution ("which coupon drove signups") was unanswerable and unrecoverable for any row already
written. Fixed with a narrow allowlist in `AuditLogWriter`; redaction stays deny-by-default, so a
new property matching a keyword is still redacted unless deliberately listed. Locked in by four
tests asserting both directions (secrets still redacted, business codes preserved). Re-verified
live: renewals now record `"CouponCode":"SUMMER20"`.

## Member Experience Engine readiness

| Capability | Readiness | Evidence / gap |
|---|---|---|
| **Version history** | Partial | AuditLog reconstructs any command-driven change. No first-class per-entity version table; `DataBefore` exists on `AuditLog` but is never populated — only `DataAfter` is written. Populating `DataBefore` is the cheapest upgrade path. |
| **Timeline** | Good raw material | Every member-facing fact is already timestamped and append-only: `AttendanceRecord`, `MemberMeasurement.MeasuredOn`, `ProgressPhoto`, `MedicalNote`, `WorkoutLog`, `MealEntry`, `WaterLog`, `Invoice`/`Payment`, `MemberMembership` periods. A timeline is a merge-and-sort over existing tables — no schema change required. |
| **Goals** | **Not modeled** | No goal/target entity anywhere. Genuinely new work: needs a `MemberGoal` (metric, target value, target date, status) plus progress evaluation against `MemberMeasurement`. |
| **Assessments** | Partial | `MemberMeasurement` is a real repeated-assessment table (weight, body fat, 5 circumferences, notes, dated). `TrainerRating` captures service quality. Missing: fitness-test results (strength/endurance benchmarks) and any assessment *template* concept. |
| **Historical analytics** | Good foundation | All member activity is append-only and dated; measurements are never overwritten (each is a new row). Aggregate reporting exists for 8 domains. Gap: Workouts/Nutrition still have no aggregate reporting (the Phase 8 Foundation verdict stands). |
| **Collaboration** | Partial | `TrainerAssignment` links members to trainers; `MedicalNote` and `LeadActivity` carry authored notes with `CreatedByUserId`. Missing: threaded comments, shared plan editing, and notification-on-mention. |

## AI-platform data readiness (storage only — no AI implemented)

| Use case | Data present today | What is missing |
|---|---|---|
| **Churn analysis** | The strongest case. Attendance is per-visit and dated; membership periods carry start/end/status/freeze; invoices carry due dates and payment timing; renewals-vs-expiries are reconstructable from AuditLog. Declining visit frequency, late payments, and freeze usage — the classic churn signals — are all already captured. | A labeled outcome table (churned vs retained per period) and feature extraction. No schema gap. |
| **Predictions** (revenue, capacity) | Payments are dated and status-tagged; attendance supports peak-hour analysis (already reported); membership expiries are forward-looking. | Nothing structural. |
| **Recommendations** (plans, classes) | Workout logs, diet plans, meal entries, and trainer assignments are all per-member and dated. | Item-level catalog metadata is thin (exercises/foods lack rich tagging), which limits content-based recommendation quality. |
| **Coaching** | Measurements over time + workout logs give a real progress signal. | No goals to coach *toward* (see MEE table), and no assessment templates. |

## Honest cross-cutting gaps

1. **Overwrite-in-place status fields**: `Lead.Stage`, `Member.Status`, `MemberMembership.Status`,
   `WorkOrder.Status`, `Asset.Status` are all updated in place. History survives only via
   AuditLog reconstruction, not via a queryable state-transition table. Fine today; a
   `StatusTransition` projection would make trend queries cheap.
2. **`AuditLog.DataBefore` is dead weight** — the column exists and is always null. Either
   populate it or drop it; leaving it suggests a before/after capability that isn't there.
3. **Hard deletes exist in two places**: `SetRolePermissionCommand` removes `RolePermission`
   rows, and the inventory import handler removes items on rollback. Both are defensible
   (permission grants are current-state by nature; import rollback should undo cleanly), but
   they are the only spots where history is genuinely gone rather than reconstructable.
4. **No PII/retention policy**: preparing to hold years of member history implies deciding
   retention windows and anonymization — unaddressed, and it interacts with the still-open
   medical-note encryption item.

## Verdict

The platform is **ready to have the Member Experience Engine built on top of it** without
schema-level refactoring: timelines, assessments, and historical analytics all sit on existing
append-only, timestamped, tenant-scoped data. Goals are the one genuinely absent MEE primitive.

For AI, **churn analysis is the best-supported first use case** — the signals already exist and
have been accumulating since seeding. Nothing in this phase implemented AI, per the guide.
