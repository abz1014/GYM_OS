using System.Text.Json;
using GymOS.Application.Modules.Migration.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
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
/// AttendanceImportEntityHandler backfills historical check-ins at their original timestamp,
/// deliberately bypassing CheckInCommand's live dashboard notification and "now"-only timestamp
/// (see ImportAttendanceRecordCommand's doc comment). AttendanceRecord has no soft-state field, so
/// unlike Membership's Cancelled status, rollback here is a hard delete — these tests confirm both
/// the historical timestamp round-trips exactly and that rollback actually removes the row.
/// </summary>
public class AttendanceImportEntityHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task A_valid_row_commits_a_record_at_its_historical_timestamp_and_rollback_deletes_it()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = ctx.MemberEmail,
            ["CheckInAt"] = "2025-11-03T08:15:00Z",
            ["CheckOutAt"] = "2025-11-03T09:20:00Z"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));
        await SendAsync(new CommitImportJobCommand(jobId, ctx.BranchId));

        Guid recordId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
            job.Status.ShouldBe(ImportStatus.Completed);

            var record = await db.AttendanceRecords.SingleAsync(a => a.MemberId == ctx.MemberId);
            record.CheckInAt.ShouldBe(DateTimeOffset.Parse("2025-11-03T08:15:00Z"));
            record.CheckOutAt.ShouldBe(DateTimeOffset.Parse("2025-11-03T09:20:00Z"));
            record.Method.ShouldBe(AttendanceMethod.Manual);
            record.RecordedByUserId.ShouldBeNull();
            recordId = record.Id;
        }

        await SendAsync(new RollbackImportJobCommand(jobId));

        using var scope2 = CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db2.AttendanceRecords.AnyAsync(a => a.Id == recordId)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_checkout_before_checkin_is_invalid()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = ctx.MemberEmail,
            ["CheckInAt"] = "2025-11-03T08:15:00Z",
            ["CheckOutAt"] = "2025-11-03T07:00:00Z"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
        job.ErrorRows.ShouldBe(1);

        var row = await db.ImportRows.SingleAsync(r => r.ImportJobId == jobId);
        row.ValidationErrors.ShouldBe("CheckOutAt cannot be before CheckInAt.");
    }

    [Fact]
    public async Task A_second_row_with_the_same_member_and_checkin_time_is_a_duplicate()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                TenantId = ctx.TenantId,
                BranchId = ctx.BranchId,
                MemberId = ctx.MemberId,
                CheckInAt = DateTimeOffset.Parse("2025-11-03T08:15:00Z"),
                Method = AttendanceMethod.Manual
            });
            await db.SaveChangesAsync();
        }

        var jobId = await SeedJobAsync(ctx.TenantId, new Dictionary<string, string>
        {
            ["MemberEmail"] = ctx.MemberEmail,
            ["CheckInAt"] = "2025-11-03T08:15:00Z"
        });

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope2 = CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var job = await db2.ImportJobs.SingleAsync(j => j.Id == jobId);
        job.DuplicateRows.ShouldBe(1);
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
            EntityType = ImportEntityType.Attendance,
            FileName = "attendance.csv",
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
