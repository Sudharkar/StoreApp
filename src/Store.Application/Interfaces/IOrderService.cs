using Store.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Interfaces
{
    public interface IOrderService
    {
        // Returns (order, created). created=false means an idempotent replay (original order returned).
        Task<(OrderDto Order, bool Created)> PlaceOrderAsync(
            int customerId, string? idempotencyKey, CreateOrderRequest request, CancellationToken ct = default);

        Task<PagedResult<OrderDto>> GetMyOrdersAsync(int customerId, int page, int pageSize, CancellationToken ct = default);
        Task<PagedResult<OrderDto>> GetAllOrdersAsync(int page, int pageSize, CancellationToken ct = default);
        Task<OrderDto> GetByIdAsync(int orderId, int requesterId, bool isAdmin, CancellationToken ct = default);
        Task<OrderDto> UpdateStatusAsync(int orderId, string newStatus, int requesterId, bool isAdmin, CancellationToken ct = default);

        // Rule 6: used by the background service
        Task<int> CancelExpiredPendingOrdersAsync(TimeSpan maxAge, CancellationToken ct = default);
    }
}
