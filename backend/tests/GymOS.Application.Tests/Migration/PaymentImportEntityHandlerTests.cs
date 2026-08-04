using System.Text.Json;
using GymOS.Application.Modules.Migration.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Migration;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Migration;

/// <summary>
/// PaymentImportEntityHandler creates a minimal paid invoice-of-record via the shared
/// CreateInvoiceCommand and then records the payment via ImportPaymentCommand rather than the live
/// RecordPaymentCommand (no gateway charge for a payment that already happened — see that
/// command's doc comment). Rollback deletes the synthetic invoice, relying on the Invoice.Payments/
/// Lines cascade-delete configuration rather than a soft-state flag, since PaymentStatus has no
/// Cancelled/Voided value.
/// </summary>
public class PaymentImportEntityHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task A_valid_row_commits_a_paid_invoice_and_rollback_removes_it_entirely()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = ctx.MemberEmail,
            ["Amount"] = "99.50",
            ["PaidAt"] = "2025-10-01T14:00:00Z",
            ["Method"] = "Cash",
            ["Notes"] = "Legacy POS receipt #4821"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));
        await SendAsync(new CommitImportJobCommand(jobId, ctx.BranchId));

        Guid invoiceId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
            job.Status.ShouldBe(ImportStatus.Completed);

            var invoice = await db.Invoices.Include(i => i.Payments).Include(i => i.Lines).SingleAsync(i => i.MemberId == ctx.MemberId);
            invoice.Status.ShouldBe(InvoiceStatus.Paid);
            invoice.TotalAmount.ShouldBe(99.50m);
            invoice.Lines.ShouldHaveSingleItem();
            invoice.Payments.ShouldHaveSingleItem();

            var payment = invoice.Payments.Single();
            payment.Amount.ShouldBe(99.50m);
            payment.Method.ShouldBe(PaymentMethod.Cash);
            payment.PaidAt.ShouldBe(DateTimeOffset.Parse("2025-10-01T14:00:00Z"));
            payment.ReceivedByUserId.ShouldBeNull();
            payment.Status.ShouldBe(PaymentStatus.Completed);

            invoiceId = invoice.Id;
        }

        await SendAsync(new RollbackImportJobCommand(jobId));

        using var scope2 = CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db2.Invoices.AnyAsync(i => i.Id == invoiceId)).ShouldBeFalse();
        (await db2.Payments.AnyAsync(p => p.InvoiceId == invoiceId)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_invalid_payment_method_is_rejected()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = ctx.MemberEmail,
            ["Amount"] = "99.50",
            ["PaidAt"] = "2025-10-01T14:00:00Z",
            ["Method"] = "Bitcoin"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
        job.ErrorRows.ShouldBe(1);

        (await db.Invoices.AnyAsync(i => i.MemberId == ctx.MemberId)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_member_email_is_invalid()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = "nobody@example.com",
            ["Amount"] = "99.50",
            ["PaidAt"] = "2025-10-01T14:00:00Z",
            ["Method"] = "Cash"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var row = await db.ImportRows.SingleAsync(r => r.ImportJobId == jobId);
        row.ValidationErrors.ShouldNotBeNull().ShouldContain("No member found");
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<Guid> SeedJobAsync(Guid tenantId, Dictionary<string, string> row)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var job = new ImportJob
        {
            TenantId = tenantId,
            EntityType = ImportEntityType.Payment,
            FileName = "payments.csv",
            FileUrl = "local://unused",
            Status = ImportStatus.Uploaded,
            TotalRows = 1
        };
        db.ImportJobs.Add(job);

        foreach (var field in row.Keys)
        {
            db.ImportFieldMappings.Add(new ImportFieldMapping { ImportJobId = job.Id, SourceColumnName = field, TargetFieldName = field });
        }

        db.ImportRows.Add(new ImportRow
        {
            ImportJobId = job.Id,
            RowNumber = 1,
            RawDataJson = JsonSerializer.Serialize(row),
            Status = ImportRowStatus.Pending
        });

        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, string MemberEmail, Guid StaffUserId)> SeedAsync()
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

        var memberEmail = $"{Guid.NewGuid():N}@example.com";
        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = memberEmail,
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, memberEmail, staffUser.Id);
    }
}
