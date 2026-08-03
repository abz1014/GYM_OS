using FluentValidation;
using GymOS.Application.Modules.Maintenance.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Equipment;
using GymOS.Domain.Maintenance;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Maintenance;

/// <summary>
/// VerifyWorkOrderCommand's core business rule: approving a work order tied to a recurring
/// maintenance schedule must restore the asset, close its open downtime, AND advance the schedule's
/// next due date — miss any one of those and the asset silently drifts out of its inspection cycle.
/// Rejecting sends it back to InProgress rather than closing it out.
/// </summary>
public class VerifyWorkOrderCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Approving_a_schedule_linked_work_order_restores_asset_closes_downtime_and_advances_schedule()
    {
        var ctx = await SeedAsync(linkToSchedule: true);
        CurrentUser.TenantId = ctx.TenantId;

        await SendAsync(new VerifyWorkOrderCommand(ctx.WorkOrderId, Approved: true, Cost: 75m, Notes: "Fixed", NextDueDate: new DateOnly(2026, 6, 1)));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var workOrder = await db.WorkOrders.SingleAsync(w => w.Id == ctx.WorkOrderId);
        workOrder.Status.ShouldBe(WorkOrderStatus.Completed);
        workOrder.Cost.ShouldBe(75m);

        var asset = await db.Assets.SingleAsync(a => a.Id == ctx.AssetId);
        asset.Status.ShouldBe(AssetStatus.Active);

        var downtime = await db.DowntimeLogs.SingleAsync(d => d.WorkOrderId == ctx.WorkOrderId);
        downtime.EndedAt.ShouldNotBeNull();

        var schedule = await db.MaintenanceSchedules.SingleAsync(s => s.Id == ctx.ScheduleId);
        schedule.NextDueDate.ShouldBe(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public async Task Approving_a_schedule_linked_work_order_without_a_next_due_date_is_rejected()
    {
        var ctx = await SeedAsync(linkToSchedule: true);
        CurrentUser.TenantId = ctx.TenantId;

        var act = () => SendAsync(new VerifyWorkOrderCommand(ctx.WorkOrderId, Approved: true, Cost: 75m, Notes: null, NextDueDate: null));

        (await Should.ThrowAsync<ValidationException>(act)).Message.ShouldContain("provide its next due date");
    }

    [Fact]
    public async Task Rejecting_sends_the_work_order_back_to_in_progress_and_leaves_the_asset_under_maintenance()
    {
        var ctx = await SeedAsync(linkToSchedule: false);
        CurrentUser.TenantId = ctx.TenantId;

        await SendAsync(new VerifyWorkOrderCommand(ctx.WorkOrderId, Approved: false, Cost: null, Notes: "Not fixed yet", NextDueDate: null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var workOrder = await db.WorkOrders.SingleAsync(w => w.Id == ctx.WorkOrderId);
        workOrder.Status.ShouldBe(WorkOrderStatus.InProgress);

        var asset = await db.Assets.SingleAsync(a => a.Id == ctx.AssetId);
        asset.Status.ShouldBe(AssetStatus.UnderMaintenance);
    }

    private async Task<(Guid TenantId, Guid AssetId, Guid WorkOrderId, Guid? ScheduleId)> SeedAsync(bool linkToSchedule)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var asset = new Asset
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            AssetTag = "EQ-0001",
            Name = "Treadmill",
            QrCodeToken = Guid.NewGuid().ToString("N"),
            Status = AssetStatus.UnderMaintenance
        };
        db.Assets.Add(asset);

        MaintenanceSchedule? schedule = null;
        if (linkToSchedule)
        {
            schedule = new MaintenanceSchedule { AssetId = asset.Id, RecurrenceRule = "Monthly", NextDueDate = new DateOnly(2026, 5, 1) };
            db.MaintenanceSchedules.Add(schedule);
        }

        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            AssetId = asset.Id,
            MaintenanceScheduleId = schedule?.Id,
            Type = WorkOrderType.Corrective,
            Status = WorkOrderStatus.PendingVerification,
            Title = "Belt replacement"
        };
        db.WorkOrders.Add(workOrder);

        db.DowntimeLogs.Add(new DowntimeLog
        {
            AssetId = asset.Id,
            WorkOrderId = workOrder.Id,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        await db.SaveChangesAsync();
        return (tenant.Id, asset.Id, workOrder.Id, schedule?.Id);
    }
}
