using GymOS.Application.Modules.Reports.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Crm;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Reports;

/// <summary>
/// Source resolution priority (a direct member referral beats a CRM lead source, which beats
/// "unattributed"), and that only completed payments count toward LTV — a pending/failed payment
/// must not inflate a channel's apparent value.
/// </summary>
public class GetLtvByAcquisitionSourceQueryTests : ApplicationTestBase
{
    [Fact]
    public async Task Members_are_grouped_by_referral_lead_source_or_unattributed_with_only_completed_revenue()
    {
        var (tenantId, branchId, userId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantId, userId);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var referrer = NewMember(tenantId, branchId, "Referrer", "Existing");
        db.Members.Add(referrer);

        var referred = NewMember(tenantId, branchId, "Referred", "ByFriend");
        referred.ReferredByMemberId = referrer.Id;
        db.Members.Add(referred);
        AddCompletedPayment(db, tenantId, branchId, referred.Id, 100m);

        var fromWebsiteLead = NewMember(tenantId, branchId, "Website", "Convert");
        db.Members.Add(fromWebsiteLead);
        db.Leads.Add(new Lead
        {
            TenantId = tenantId,
            BranchId = branchId,
            FirstName = "Website",
            LastName = "Convert",
            Email = fromWebsiteLead.Email,
            Source = LeadSource.Website,
            Stage = LeadStage.Member,
            ConvertedMemberId = fromWebsiteLead.Id
        });
        AddCompletedPayment(db, tenantId, branchId, fromWebsiteLead.Id, 50m);
        AddPendingPayment(db, tenantId, branchId, fromWebsiteLead.Id, 999m); // must NOT count

        var unattributed = NewMember(tenantId, branchId, "Walk", "In");
        db.Members.Add(unattributed);
        AddCompletedPayment(db, tenantId, branchId, unattributed.Id, 30m);

        await db.SaveChangesAsync();

        var rows = await SendAsync(new GetLtvByAcquisitionSourceQuery());

        rows.Count.ShouldBe(3); // Referral (Member), Website, Direct/Unattributed

        var referralRow = rows.Single(r => r.Source == "Referral (Member)");
        referralRow.MemberCount.ShouldBe(1); // only "referred" carries ReferredByMemberId
        referralRow.TotalRevenue.ShouldBe(100m);

        var websiteRow = rows.Single(r => r.Source == "Website");
        websiteRow.MemberCount.ShouldBe(1);
        websiteRow.TotalRevenue.ShouldBe(50m); // pending payment excluded

        var unattributedRow = rows.Single(r => r.Source == "Direct / Unattributed");
        // Both "referrer" (nobody referred them) and "unattributed" fall here.
        unattributedRow.MemberCount.ShouldBe(2);
        unattributedRow.TotalRevenue.ShouldBe(30m);
    }

    private static Member NewMember(Guid tenantId, Guid branchId, string first, string last) => new()
    {
        TenantId = tenantId,
        BranchId = branchId,
        MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
        FirstName = first,
        LastName = last,
        Email = $"{Guid.NewGuid():N}@example.com",
        JoinDate = new DateOnly(2025, 6, 1),
        QrCodeToken = Guid.NewGuid().ToString("N")
    };

    private static void AddCompletedPayment(GymOsDbContext db, Guid tenantId, Guid branchId, Guid memberId, decimal amount)
        => AddInvoiceWithPayment(db, tenantId, branchId, memberId, amount, PaymentStatus.Completed);

    private static void AddPendingPayment(GymOsDbContext db, Guid tenantId, Guid branchId, Guid memberId, decimal amount)
        => AddInvoiceWithPayment(db, tenantId, branchId, memberId, amount, PaymentStatus.Pending);

    private static void AddInvoiceWithPayment(
        GymOsDbContext db, Guid tenantId, Guid branchId, Guid memberId, decimal amount, PaymentStatus status)
    {
        var invoice = new Invoice
        {
            TenantId = tenantId,
            BranchId = branchId,
            MemberId = memberId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
            IssueDate = new DateOnly(2025, 6, 1),
            DueDate = new DateOnly(2025, 6, 8),
            Status = InvoiceStatus.Paid,
            Subtotal = amount,
            TotalAmount = amount,
            Currency = "USD"
        };
        invoice.Payments.Add(new Payment
        {
            Method = PaymentMethod.Card,
            Amount = amount,
            PaidAt = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero),
            Status = status
        });
        db.Invoices.Add(invoice);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid UserId)> SeedTenantAsync()
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

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, staffUser.Id);
    }
}
