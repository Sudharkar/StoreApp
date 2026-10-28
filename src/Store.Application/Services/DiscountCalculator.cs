using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Services
{
    // Rule 5: pure logic (no DB), so it is easy to unit test.
    public static class DiscountCalculator
    {
        public const decimal BulkThreshold = 10000m;
        public const decimal BulkRate = 0.05m;
        public const decimal LoyaltyRate = 0.10m;
        public const int LoyaltyEveryNthOrder = 5;

        // orderNumberForCustomer = how many orders this customer will have including this one
        public static (decimal Discount, decimal Total) Calculate(decimal subTotal, int orderNumberForCustomer)
        {
            subTotal = Math.Round(subTotal, 2, MidpointRounding.AwayFromZero);
            var running = subTotal;

            // 5% off when SubTotal is above 10,000
            if (subTotal > BulkThreshold)
                running -= Math.Round(running * BulkRate, 2, MidpointRounding.AwayFromZero);

            // extra 10% off on every 5th order (applied on the already discounted amount)
            if (orderNumberForCustomer > 0 && orderNumberForCustomer % LoyaltyEveryNthOrder == 0)
                running -= Math.Round(running * LoyaltyRate, 2, MidpointRounding.AwayFromZero);

            var total = Math.Round(running, 2, MidpointRounding.AwayFromZero);
            var discount = Math.Round(subTotal - total, 2, MidpointRounding.AwayFromZero);
            return (discount, total);
        }
    }
}
