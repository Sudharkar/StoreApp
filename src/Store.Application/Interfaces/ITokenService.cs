using Store.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Interfaces
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
        string GenerateRefreshToken();
    }
}