using Store.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Interfaces
{
    public interface IProductService
    {
        Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, CancellationToken ct = default);
        Task<ProductDto> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
        Task<ProductDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default);
    }
}
