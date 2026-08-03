using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Infrastructure.Messaging;

/// <summary>
/// Demo senders — none of these call a real provider. Each writes to NotificationLog instead,
/// which the frontend surfaces as an in-app "Dev Mailbox" so flows like forgot-password are fully
/// demoable without SMTP/Twilio/WhatsApp Business API credentials.
/// </summary>
internal static class TenantResolution
{
    /// <summary>
    /// Anonymous flows (forgot-password) and background jobs have no ambient JWT, so
    /// ICurrentUserService.TenantId is null. This MVP only ever seeds one tenant, so falling
    /// back to "the only tenant in the DB" is a safe simplification — a real multi-tenant
    /// deployment would resolve the tenant from the anonymous request itself (e.g. subdomain).
    /// </summary>
    public static async Task<Guid> ResolveAsync(IApplicationDbContext db, ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not null)
        {
            return currentUser.TenantId.Value;
        }

        return await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).FirstOrDefaultAsync(cancellationToken);
    }
}

public class NoOpEmailSender(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider) : IEmailSender
{
    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        db.NotificationLogs.Add(new NotificationLog
        {
            TenantId = await TenantResolution.ResolveAsync(db, currentUser, cancellationToken),
            Channel = NotificationChannel.Email,
            RecipientAddress = toAddress,
            Subject = subject,
            Body = body,
            SentAt = dateTimeProvider.UtcNow,
            Success = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}

public class NoOpSmsSender(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider) : ISmsSender
{
    public async Task SendAsync(string toPhoneNumber, string body, CancellationToken cancellationToken = default)
    {
        db.NotificationLogs.Add(new NotificationLog
        {
            TenantId = await TenantResolution.ResolveAsync(db, currentUser, cancellationToken),
            Channel = NotificationChannel.Sms,
            RecipientAddress = toPhoneNumber,
            Subject = "SMS",
            Body = body,
            SentAt = dateTimeProvider.UtcNow,
            Success = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}

public class NoOpWhatsAppSender(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider) : IWhatsAppSender
{
    public async Task SendAsync(string toPhoneNumber, string body, CancellationToken cancellationToken = default)
    {
        db.NotificationLogs.Add(new NotificationLog
        {
            TenantId = await TenantResolution.ResolveAsync(db, currentUser, cancellationToken),
            Channel = NotificationChannel.WhatsApp,
            RecipientAddress = toPhoneNumber,
            Subject = "WhatsApp",
            Body = body,
            SentAt = dateTimeProvider.UtcNow,
            Success = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
