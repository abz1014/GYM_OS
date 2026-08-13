using GymOS.Domain.Common;

namespace GymOS.Domain.Trainers;

public class TrainerSession : BaseEntity, ITenantScoped
{
    /*
     * Tenant-scoped, and it was not.
     *
     * The EF global query filter attaches to an entity only if the entity carries the column it
     * filters on. This table had neither TenantId nor BranchId, so no filter could apply and the
     * handlers loaded rows by raw id — a cross-tenant read AND write on any multi-tenant deploy.
     * Invisible on a single-tenant seed, which is exactly why it survived this long.
     *
     * TenantId (not BranchId): a trainer belongs to a branch, but their assignments, sessions and
     * commission history follow the trainer rather than a site, and staff who manage trainers are
     * branch-mobile. Tenant is the boundary that must never be crossed; branch is reached through
     * Trainer, which is IBranchScoped already.
     */
    public Guid TenantId { get; set; }

    public Guid TrainerAssignmentId { get; set; }

    public TrainerAssignment? TrainerAssignment { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public int DurationMinutes { get; set; }

    public TrainerSessionStatus Status { get; set; } = TrainerSessionStatus.Scheduled;

    public string? Notes { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
