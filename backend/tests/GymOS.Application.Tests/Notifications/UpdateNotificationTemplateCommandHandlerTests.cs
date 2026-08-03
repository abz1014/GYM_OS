using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Notifications.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Notifications;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Notifications;

/// <summary>
/// UpdateNotificationTemplateCommand's core business rule: a template can be edited (subject, body,
/// active flag) only if it actually exists — the demo "dev mailbox" pipeline reads these templates
/// by TenantId+Code at send time, so a silently-created phantom template would fail there instead.
/// </summary>
public class UpdateNotificationTemplateCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Updating_an_existing_template_persists_subject_body_and_active_flag()
    {
        var (tenantId, templateId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        await SendAsync(new UpdateNotificationTemplateCommand(templateId, "New Subject", "New body {{Name}}", IsActive: false));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var template = await db.NotificationTemplates.SingleAsync(t => t.Id == templateId);
        template.Subject.ShouldBe("New Subject");
        template.BodyTemplate.ShouldBe("New body {{Name}}");
        template.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Updating_a_nonexistent_template_is_rejected()
    {
        var (tenantId, _) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        var act = () => SendAsync(new UpdateNotificationTemplateCommand(Guid.NewGuid(), "Subject", "Body", true));

        await Should.ThrowAsync<NotFoundException>(act);
    }

    private async Task<(Guid TenantId, Guid TemplateId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var template = new NotificationTemplate
        {
            TenantId = tenant.Id,
            Code = "MembershipExpiring",
            Category = NotificationCategory.MembershipExpiry,
            Channel = NotificationChannel.Email,
            Subject = "Old Subject",
            BodyTemplate = "Old body",
            IsActive = true
        };
        db.NotificationTemplates.Add(template);

        await db.SaveChangesAsync();
        return (tenant.Id, template.Id);
    }
}
