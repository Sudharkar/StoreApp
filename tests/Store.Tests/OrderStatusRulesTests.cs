using FluentAssertions;
using Store.Application.Services;
using Store.Domain.Enums;
using Xunit;

namespace Store.Tests;

// Rule 4: status transitions
public class OrderStatusRulesTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    public void Allowed_transitions_return_true(OrderStatus from, OrderStatus to)
        => OrderStatusRules.CanTransition(from, to).Should().BeTrue();

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    public void Invalid_transitions_return_false(OrderStatus from, OrderStatus to)
        => OrderStatusRules.CanTransition(from, to).Should().BeFalse();
}