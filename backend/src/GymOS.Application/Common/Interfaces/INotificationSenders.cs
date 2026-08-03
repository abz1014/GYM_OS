namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Demo implementations of these three log to NotificationLog (the in-app "Dev Mailbox") instead
/// of actually sending, so forgot-password/reminders/alerts are fully demoable without SMTP,
/// Twilio, or WhatsApp Business API credentials. Real senders plug in later behind the same
/// interfaces via appsettings config.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);
}

public interface ISmsSender
{
    Task SendAsync(string toPhoneNumber, string body, CancellationToken cancellationToken = default);
}

public interface IWhatsAppSender
{
    Task SendAsync(string toPhoneNumber, string body, CancellationToken cancellationToken = default);
}
