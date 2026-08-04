namespace GymOS.Domain.Notifications;

public enum NotificationCategory
{
    MembershipExpiry,
    Maintenance,
    Birthday,
    FollowUp,
    LowStock,

    /// <summary>A recurring membership payment was declined — the dunning sequence.</summary>
    PaymentFailed,

    /// <summary>A member has stopped showing up and is at risk of quietly churning.</summary>
    ChurnRisk,

    /// <summary>A class the member booked is starting soon.</summary>
    ClassReminder
}
