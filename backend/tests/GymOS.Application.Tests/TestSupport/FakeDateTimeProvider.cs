using GymOS.Application.Common.Interfaces;

namespace GymOS.Application.Tests.TestSupport;

public class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
}
