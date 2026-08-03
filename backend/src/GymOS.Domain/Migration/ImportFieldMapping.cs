using GymOS.Domain.Common;

namespace GymOS.Domain.Migration;

public class ImportFieldMapping : BaseEntity
{
    public Guid ImportJobId { get; set; }

    public ImportJob? ImportJob { get; set; }

    public string SourceColumnName { get; set; } = string.Empty;

    public string TargetFieldName { get; set; } = string.Empty;
}
