using GymOS.Domain.Common;

namespace GymOS.Domain.Migration;

public class ImportRow : BaseEntity
{
    public Guid ImportJobId { get; set; }

    public ImportJob? ImportJob { get; set; }

    public int RowNumber { get; set; }

    public string RawDataJson { get; set; } = string.Empty;

    public string? ValidationErrors { get; set; }

    public bool IsDuplicate { get; set; }

    public Guid? DuplicateOfEntityId { get; set; }

    public Guid? MappedEntityId { get; set; }

    public ImportRowStatus Status { get; set; } = ImportRowStatus.Pending;
}
