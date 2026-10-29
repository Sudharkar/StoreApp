using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Application.DTOs;
using Store.Application.Interfaces;

namespace Store.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet("top-customers")]
    public async Task<ActionResult<List<TopCustomerDto>>> TopCustomers(
        [FromQuery] int days = 30, [FromQuery] int top = 5, CancellationToken ct = default)
        => Ok(await _reports.GetTopCustomersAsync(days, top, ct));
}