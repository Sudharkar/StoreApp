using Store.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Interfaces
{
    public interface IReportService
    {
        Task<List<TopCustomerDto>> GetTopCustomersAsync(int days, int top, CancellationToken ct = default);
    }
}
