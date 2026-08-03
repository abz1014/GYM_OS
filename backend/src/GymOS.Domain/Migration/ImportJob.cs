using GymOS.Domain.Common;

namespace GymOS.Domain.Migration;

public class ImportJob : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public ImportEntityType EntityType { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public ImportStatus Status { get; set; } = ImportStatus.Uploaded;

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int DuplicateRows { get; set; }

    public int ErrorRows { get; set; }

    public DateTimeOffset? CommittedAt { get; set; }

    public DateTimeOffset? RolledBackAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ICollection<ImportRow> Rows { get; set; } = [];

    public ICollection<ImportFieldMapping> FieldMappings { get; set; } = [];
}
