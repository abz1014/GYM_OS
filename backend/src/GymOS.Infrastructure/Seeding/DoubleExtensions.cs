namespace GymOS.Infrastructure.Seeding;

internal static class DoubleExtensions
{
    public static decimal ToDecimalSafe(this double value) => (decimal)value;
}
