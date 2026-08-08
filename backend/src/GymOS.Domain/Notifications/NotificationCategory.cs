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
    ClassReminder,

    /// <summary>An automated nurture nudge to a cold lead nobody has followed up with yet.</summary>
    LeadDrip,

    /// <summary>Their coach has written to them. The one notification in this list a member asked for
    /// by starting a conversation, rather than one the gym decided to send.</summary>
    CoachReply
}
