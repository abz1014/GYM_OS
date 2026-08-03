using GymOS.Domain.Inventory;
using Shouldly;

namespace GymOS.Domain.Tests.Inventory;

public class InventoryItemTests
{
    [Fact]
    public void IsLowStock_is_true_when_exactly_at_the_reorder_level()
    {
        var item = new InventoryItem { QuantityOnHand = 5, ReorderLevel = 5 };

        item.IsLowStock.ShouldBeTrue();
    }

    [Fact]
    public void IsLowStock_is_true_when_below_the_reorder_level()
    {
        var item = new InventoryItem { QuantityOnHand = 2, ReorderLevel = 5 };

        item.IsLowStock.ShouldBeTrue();
    }

    [Fact]
    public void IsLowStock_is_false_when_above_the_reorder_level()
    {
        var item = new InventoryItem { QuantityOnHand = 6, ReorderLevel = 5 };

        item.IsLowStock.ShouldBeFalse();
    }
}
