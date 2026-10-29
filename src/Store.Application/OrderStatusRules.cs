using Store.Domain.Enums;

namespace Store.Application.Services;

// Rule 4: allowed status transitions (pure logic, easy to unit test)
public static class OrderStatusRules
{
    public static bool CanTransition(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.Pending => to is OrderStatus.Confirmed or OrderStatus.Cancelled,
        OrderStatus.Confirmed => to is OrderStatus.Shipped or OrderStatus.Cancelled,
        _ => false // Shipped and Cancelled are final
    };
}