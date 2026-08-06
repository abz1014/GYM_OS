using GymOS.Application.Modules.Settings.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Tests.Settings;

/// <summary>
/// Branch.Capacity is the denominator behind every "34 / 180" and occupancy bar in the product, so
/// the rules that matter are about the difference between a number and the absence of one. Null must
/// survive as null rather than settling to zero, a supplied figure must be a room somebody could
/// stand in, and a gym must be able to withdraw a figure it no longer trusts.
/// </summary>
public class BranchCapacityTests : ApplicationTestBase
{
    [Fact]
    public async Task A_branch_created_without_a_capacity_stores_null_rather_than_zero()
    {
        await SeedTenantAsync();

        var branchId = await SendAsync(NewBranch());

        var branch = await LoadAsync(branchId);
        // Zero would render as a closed building on the front desk. "Nobody told us" has to stay
        // distinguishable from "nobody fits".
        branch.Capacity.ShouldBeNull();
    }

    [Fact]
    public async Task A_supplied_capacity_is_stored_as_given()
    {
        await SeedTenantAsync();

        var branchId = await SendAsync(NewBranch() with { Capacity = 180 });

        (await LoadAsync(branchId)).Capacity.ShouldBe(180);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Branch.MaxCapacity + 1)]
    public async Task A_capacity_that_could_not_describe_a_building_is_rejected(int capacity)
    {
        await SeedTenantAsync();

        await Should.ThrowAsync<ValidationException>(() => SendAsync(NewBranch() with { Capacity = capacity }));
    }

    [Fact]
    public async Task Clearing_a_capacity_puts_the_branch_back_to_having_none()
    {
        await SeedTenantAsync();
        var branchId = await SendAsync(NewBranch() with { Capacity = 180 });

        await SendAsync(UpdateFor(branchId) with { Capacity = null });

        // Not 180 left in place, and not 0 — withdrawing the figure is a real edit, and every
        // consumer is built to fall back to the bare count when it is missing.
        (await LoadAsync(branchId)).Capacity.ShouldBeNull();
    }

    [Fact]
    public async Task A_capacity_can_be_corrected_upward_after_the_fact()
    {
        await SeedTenantAsync();
        var branchId = await SendAsync(NewBranch() with { Capacity = 180 });

        await SendAsync(UpdateFor(branchId) with { Capacity = 240 });

        (await LoadAsync(branchId)).Capacity.ShouldBe(240);
    }

    private static CreateBranchCommand NewBranch() =>
        new("Downtown", "1 Main St", "Lisbon", "PT", "UTC", "EUR");

    private static UpdateBranchCommand UpdateFor(Guid branchId) =>
        new(branchId, "Downtown", "1 Main St", "Lisbon", "PT", "UTC", "EUR", IsActive: true);

    private async Task SeedTenantAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
    }

    private async Task<Branch> LoadAsync(Guid branchId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.Branches.AsNoTracking().SingleAsync(b => b.Id == branchId);
    }
}
