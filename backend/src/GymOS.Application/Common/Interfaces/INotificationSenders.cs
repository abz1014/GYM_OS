namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Demo implementations of email/SMS/WhatsApp log to NotificationLog (the in-app "Dev Mailbox")
/// instead of actually sending, so forgot-password/reminders/alerts are fully demoable without SMTP,
/// Twilio, or WhatsApp Business API credentials. Real senders plug in later behind the same
/// interfaces via appsettings config.
///
/// <see cref="IInAppSender"/> is the exception and always will be: an in-app notification has no
/// external provider to stub out, so its implementation is the real one rather than a stand-in.
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

/// <summary>
/// Records a notification delivered inside the product itself. Not a stub — there is nothing to
/// stub, which makes it the only channel here that works the same in a demo and in production.
/// </summary>
public interface IInAppSender
{
    Task SendAsync(string recipientReference, string subject, string body, CancellationToken cancellationToken = default);
}
