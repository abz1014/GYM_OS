using FluentValidation;
using GymOS.Application.Modules.Billing.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Inventory;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Billing;

/// <summary>
/// Closes the POS loop: a ProductSale invoice line that names an inventory item now decrements
/// stock through the same RecordStockMovementCommand RecordPurchaseCommand uses to add it (see
/// CreateInvoiceCommand's doc comment). These tests lock down both the happy path and the
/// insufficient-stock rollback, mirroring RenewMembershipCommandHandlerTests' nested-command
/// coverage for the invoice-creation side.
/// </summary>
public class CreateInvoiceCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task A_product_sale_line_with_an_inventory_item_decrements_stock()
    {
        var ctx = await SeedAsync(quantityOnHand: 10);
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var invoiceId = await SendAsync(new CreateInvoiceCommand(
            ctx.MemberId, ctx.BranchId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 22),
            TaxAmount: 0, DiscountAmount: 0, Currency: "USD", Notes: null,
            Lines: [new CreateInvoiceLineInput(InvoiceLineItemType.ProductSale, "Protein Shake", 3, 5m, ctx.InventoryItemId)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var item = await db.InventoryItems.SingleAsync(i => i.Id == ctx.InventoryItemId);
        item.QuantityOnHand.ShouldBe(7);

        var movement = await db.StockMovements.SingleAsync(m => m.InventoryItemId == ctx.InventoryItemId);
        movement.Type.ShouldBe(StockMovementType.Out);
        movement.Quantity.ShouldBe(3);

        var invoice = await db.Invoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId);
        invoice.Lines.ShouldHaveSingleItem();
        invoice.Lines.Single().InventoryItemId.ShouldBe(ctx.InventoryItemId);
    }

    [Fact]
    public async Task Selling_more_than_is_on_hand_fails_atomically_with_no_invoice_or_stock_change()
    {
        var ctx = await SeedAsync(quantityOnHand: 2);
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var act = () => SendAsync(new CreateInvoiceCommand(
            ctx.MemberId, ctx.BranchId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 22),
            TaxAmount: 0, DiscountAmount: 0, Currency: "USD", Notes: null,
            Lines: [new CreateInvoiceLineInput(InvoiceLineItemType.ProductSale, "Protein Shake", 5, 5m, ctx.InventoryItemId)]));

        await Should.ThrowAsync<ValidationException>(act);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.Invoices.AnyAsync(i => i.MemberId == ctx.MemberId)).ShouldBeFalse();
        var item = await db.InventoryItems.SingleAsync(i => i.Id == ctx.InventoryItemId);
        item.QuantityOnHand.ShouldBe(2);
    }

    [Fact]
    public async Task A_non_product_sale_line_cannot_reference_an_inventory_item()
    {
        var ctx = await SeedAsync(quantityOnHand: 10);
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var act = () => SendAsync(new CreateInvoiceCommand(
            ctx.MemberId, ctx.BranchId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 22),
            TaxAmount: 0, DiscountAmount: 0, Currency: "USD", Notes: null,
            Lines: [new CreateInvoiceLineInput(InvoiceLineItemType.MembershipFee, "Membership", 1, 150m, ctx.InventoryItemId)]));

        await Should.ThrowAsync<ValidationException>(act);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid InventoryItemId, Guid StaffUserId)> SeedAsync(int quantityOnHand)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var staffUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Staff",
            LastName = "User"
        };
        db.Users.Add(staffUser);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = branch.Id });

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

        var item = new InventoryItem
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Sku = $"SKU-{Guid.NewGuid():N}"[..10],
            Name = "Protein Shake",
            Category = InventoryCategory.Merchandise,
            QuantityOnHand = quantityOnHand,
            ReorderLevel = 1,
            UnitCost = 2m,
            UnitPrice = 5m
        };
        db.InventoryItems.Add(item);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, item.Id, staffUser.Id);
    }
}
