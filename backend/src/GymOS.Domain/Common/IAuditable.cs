namespace GymOS.Domain.Common;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    Guid? CreatedByUserId { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    Guid? UpdatedByUserId { get; set; }
}
