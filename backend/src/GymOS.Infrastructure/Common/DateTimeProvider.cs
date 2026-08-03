using GymOS.Application.Common.Interfaces;

namespace GymOS.Infrastructure.Common;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
