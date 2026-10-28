using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;

        // Optimistic concurrency token (SQL Server rowversion)
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
