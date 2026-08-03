namespace GymOS.Domain.Notifications;

// Email/Sms/WhatsApp route through demo no-op senders in Infrastructure that log instead of
// sending - no external provider required for the MVP.
public enum NotificationChannel
{
    InApp,
    Email,
    Sms,
    WhatsApp
}
