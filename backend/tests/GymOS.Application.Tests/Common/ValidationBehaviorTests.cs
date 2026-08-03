using FluentValidation;
using GymOS.Application.Modules.Billing.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Common;

/// <summary>
/// ValidationBehavior runs before TransactionBehavior in the pipeline (TenantScope -> Logging
/// -> Validation -> Transaction -> Audit), so an invalid command should never open a transaction,
/// never reach the handler, and leave zero rows behind.
/// </summary>
public class ValidationBehaviorTests : ApplicationTestBase
{
    [Fact]
    public async Task Invalid_command_throws_before_the_handler_runs_and_writes_nothing()
    {
        var (tenantId, branchId, memberId, userId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;

        // Empty Lines and DueDate before IssueDate both violate CreateInvoiceCommandValidator.
        var act = () => SendAsync(new CreateInvoiceCommand(
            memberId, branchId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 1), 0m, 0m, "USD", null, []));

        var ex = await Should.ThrowAsync<ValidationException>(act);
        ex.Errors.ShouldNotBeEmpty();

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Invoices.AnyAsync(i => i.MemberId == memberId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Valid_command_passes_through_and_creates_the_invoice()
    {
        var (tenantId, branchId, memberId, userId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;

        var invoiceId = await SendAsync(new CreateInvoiceCommand(
            memberId, branchId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), 0m, 0m, "USD", null,
            [new CreateInvoiceLineInput(InvoiceLineItemType.Fee, "Registration fee", 1, 25m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Invoices.AnyAsync(i => i.Id == invoiceId)).ShouldBeTrue();
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid UserId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Staff",
            LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, user.Id);
    }
}
