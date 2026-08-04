using System.Globalization;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

/// <summary>
/// Legacy exports rarely include a UTC offset on their timestamps. Parsing with AssumeUniversal
/// treats a bare "2024-01-15 09:30" as already UTC rather than the importing machine's local time
/// zone — Npgsql rejects a non-zero-offset DateTimeOffset for a timestamptz column, so a plain
/// DateTimeOffset.Parse here would fail (or silently shift) depending on where the import happens
/// to run.
/// </summary>
internal static class ImportDateTimeParsing
{
    public static bool TryParseUtc(string value, out DateTimeOffset result)
        => DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
}
