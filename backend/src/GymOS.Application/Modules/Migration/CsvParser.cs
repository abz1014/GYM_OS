using System.Text;

namespace GymOS.Application.Modules.Migration;

/// <summary>Minimal RFC-4180-ish CSV reader (quoted fields, embedded commas/quotes) — no external dependency for what's otherwise a small, bounded parsing task.</summary>
public static class CsvParser
{
    public static List<string[]> ParseRows(string content)
    {
        var rows = new List<string[]>();
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");

        foreach (var line in normalized.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            rows.Add(ParseLine(line));
        }

        return rows;
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());
        return fields.ToArray();
    }
}
