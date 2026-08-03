namespace GymOS.Application.Common.Interfaces;

/// <summary>Generic tabular-data-to-file exporter shared by every report — real .xlsx generation via ClosedXML in Infrastructure, not a placeholder.</summary>
public interface IReportExporter
{
    byte[] ExportToXlsx(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows);
}
