namespace GymOS.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "GymOS";

    public string Audience { get; set; } = "GymOS.Client";

    public int AccessTokenLifetimeMinutes { get; set; } = 20;
}
