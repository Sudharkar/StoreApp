using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Application.DTOs;
using Store.Application.Interfaces;

namespace Store.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CreateOrderRequest request, CancellationToken ct)
    {
        var (order, created) = await _orders.PlaceOrderAsync(UserId, idempotencyKey, request, ct);
        if (!created) return Ok(order); // idempotent replay
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("my")]
    public async Task<ActionResult<PagedResult<OrderDto>>> My(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(await _orders.GetMyOrdersAsync(UserId, page, pageSize, ct));

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<OrderDto>>> All(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(await _orders.GetAllOrdersAsync(page, pageSize, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
        => Ok(await _orders.GetByIdAsync(id, UserId, IsAdmin, ct));

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken ct)
        => Ok(await _orders.UpdateStatusAsync(id, request.Status, UserId, IsAdmin, ct));
}