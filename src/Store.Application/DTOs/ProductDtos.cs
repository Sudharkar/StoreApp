using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Store.Application.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateProductRequest
    {
        [Required, StringLength(50)]
        public string Sku { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 10000000)]
        public decimal Price { get; set; }

        [Range(0, 1000000)]
        public int StockQuantity { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateProductRequest
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 10000000)]
        public decimal Price { get; set; }

        [Range(0, 1000000)]
        public int StockQuantity { get; set; }

        public bool IsActive { get; set; }
    }

    public class ProductQuery
    {
        public string? Name { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? InStock { get; set; }

        // name | price | stock | newest
        public string? SortBy { get; set; }
        public bool Desc { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
