using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.Enums
{
    public enum OrderStatus
    {
        Pending = 1,
        Confirmed = 2,
        Shipped = 3,
        Cancelled = 4
    }
}
