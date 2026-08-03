namespace GymOS.Shared;

public static class RoleNames
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Receptionist = "Receptionist";
    public const string Trainer = "Trainer";
    public const string Nutritionist = "Nutritionist";
    public const string Accountant = "Accountant";
    public const string Maintenance = "Maintenance";
    public const string Member = "Member";

    public static readonly IReadOnlyList<string> All =
    [
        Owner, Manager, Receptionist, Trainer, Nutritionist, Accountant, Maintenance, Member
    ];
}
