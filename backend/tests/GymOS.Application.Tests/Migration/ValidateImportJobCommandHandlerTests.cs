using System.Text.Json;
using GymOS.Application.Modules.Migration.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Migration;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Migration;

/// <summary>
/// ValidateImportJobCommand's core business rule: two rows sharing the same natural key (email,
/// SKU, ...) within the SAME file must be caught even though neither exists in the database yet —
/// the per-row handler validation alone can't see that, only the batch-wide seenKeysInBatch tracking
/// in the command itself can.
/// </summary>
public class ValidateImportJobCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task A_repeated_email_within_the_same_file_is_flagged_as_a_duplicate_of_the_earlier_row()
    {
        var (tenantId, jobId) = await SeedAsync("duplicate@example.com", "duplicate@example.com");
        CurrentUser.TenantId = tenantId;

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
        job.Status.ShouldBe(ImportStatus.Validated);
        job.ValidRows.ShouldBe(1);
        job.DuplicateRows.ShouldBe(1);
        job.ErrorRows.ShouldBe(0);

        var rows = await db.ImportRows.Where(r => r.ImportJobId == jobId).OrderBy(r => r.RowNumber).ToListAsync();
        rows[0].Status.ShouldBe(ImportRowStatus.Valid);
        rows[0].IsDuplicate.ShouldBeFalse();
        rows[1].Status.ShouldBe(ImportRowStatus.Invalid);
        rows[1].IsDuplicate.ShouldBeTrue();
        rows[1].ValidationErrors.ShouldNotBeNull().ShouldContain("Duplicate of an earlier row in this same file");
    }

    [Fact]
    public async Task Two_rows_with_distinct_emails_both_validate_clean()
    {
        var (tenantId, jobId) = await SeedAsync("first@example.com", "second@example.com");
        CurrentUser.TenantId = tenantId;

        await SendAsync(new ValidateImportJobCommand(jobId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId);
        job.ValidRows.ShouldBe(2);
        job.DuplicateRows.ShouldBe(0);
    }

    private async Task<(Guid TenantId, Guid JobId)> SeedAsync(string email1, string email2)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var job = new ImportJob
        {
            TenantId = tenant.Id,
            EntityType = ImportEntityType.Member,
            FileName = "members.csv",
            FileUrl = "local://unused",
            Status = ImportStatus.Uploaded,
            TotalRows = 2
        };
        db.ImportJobs.Add(job);

        foreach (var (source, target) in new[] { ("First", "FirstName"), ("Last", "LastName"), ("EmailAddress", "Email") })
        {
            db.ImportFieldMappings.Add(new ImportFieldMapping { ImportJobId = job.Id, SourceColumnName = source, TargetFieldName = target });
        }

        db.ImportRows.Add(new ImportRow
        {
            ImportJobId = job.Id,
            RowNumber = 1,
            RawDataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["First"] = "Ann", ["Last"] = "Lee", ["EmailAddress"] = email1 }),
            Status = ImportRowStatus.Pending
        });
        db.ImportRows.Add(new ImportRow
        {
            ImportJobId = job.Id,
            RowNumber = 2,
            RawDataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["First"] = "Bob", ["Last"] = "Ng", ["EmailAddress"] = email2 }),
            Status = ImportRowStatus.Pending
        });

        await db.SaveChangesAsync();
        return (tenant.Id, job.Id);
    }
}
