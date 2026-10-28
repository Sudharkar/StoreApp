using Store.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);
    }
}
