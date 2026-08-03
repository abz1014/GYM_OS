namespace GymOS.Shared;

public static class Guard
{
    public static T NotNull<T>(T? value, string paramName) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    public static string NotNullOrWhiteSpace(string? value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", paramName)
            : value;

    public static Guid NotEmpty(Guid value, string paramName)
        => value == Guid.Empty ? throw new ArgumentException("Value cannot be an empty Guid.", paramName) : value;
}
