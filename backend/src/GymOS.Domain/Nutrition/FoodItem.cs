using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

public class FoodItem : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal CaloriesPerServing { get; set; }

    public decimal ProteinG { get; set; }

    public decimal CarbsG { get; set; }

    public decimal FatG { get; set; }

    public string ServingSizeDescription { get; set; } = string.Empty;
}
