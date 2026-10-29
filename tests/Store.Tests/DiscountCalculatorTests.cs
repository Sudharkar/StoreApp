using FluentAssertions;
using Store.Application.Services;
using Xunit;

namespace Store.Tests;

// Rule 5: discount rules
public class DiscountCalculatorTests
{
    [Fact]
    public void Small_order_gets_no_discount()
    {
        var (discount, total) = DiscountCalculator.Calculate(5000m, 1);
        discount.Should().Be(0m);
        total.Should().Be(5000m);
    }

    [Fact]
    public void SubTotal_exactly_10000_gets_no_bulk_discount()
    {
        var (discount, total) = DiscountCalculator.Calculate(10000m, 1);
        discount.Should().Be(0m);
        total.Should().Be(10000m);
    }

    [Fact]
    public void SubTotal_above_10000_gets_5_percent()
    {
        var (discount, total) = DiscountCalculator.Calculate(20000m, 1);
        discount.Should().Be(1000m);
        total.Should().Be(19000m);
    }

    [Fact]
    public void Fifth_order_gets_extra_10_percent()
    {
        var (discount, total) = DiscountCalculator.Calculate(5000m, 5);
        discount.Should().Be(500m);
        total.Should().Be(4500m);
    }

    [Fact]
    public void Fifth_order_above_10000_gets_both_discounts()
    {
        // 20000 -> 5% = 19000 -> 10% = 17100
        var (discount, total) = DiscountCalculator.Calculate(20000m, 5);
        discount.Should().Be(2900m);
        total.Should().Be(17100m);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Non_fifth_orders_get_no_loyalty_discount(int orderNumber)
    {
        var (discount, _) = DiscountCalculator.Calculate(1000m, orderNumber);
        discount.Should().Be(0m);
    }

    [Fact]
    public void Tenth_order_also_gets_loyalty_discount()
    {
        var (discount, total) = DiscountCalculator.Calculate(1000m, 10);
        discount.Should().Be(100m);
        total.Should().Be(900m);
    }

    [Fact]
    public void Result_is_rounded_to_2_decimals()
    {
        // 10% of 333.33 = 33.333 -> 33.33
        var (discount, total) = DiscountCalculator.Calculate(333.33m, 5);
        discount.Should().Be(33.33m);
        total.Should().Be(300.00m);
    }
}