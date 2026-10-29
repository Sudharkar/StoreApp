using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Store.Application.DTOs;
using Store.Application.Exceptions;
using Store.Application.Interfaces;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Auth;
using Store.Infrastructure.Data;

namespace Store.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly StoreDbContext _db;
    private readonly ITokenService _tokens;
    private readonly JwtSettings _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(StoreDbContext db, ITokenService tokens,
        IOptions<JwtSettings> jwt, ILogger<AuthService> logger)
    {
        _db = db;
        _tokens = tokens;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Email is already registered.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Customer // role is never taken from the client
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // two parallel registrations with the same email: unique index protects us
            throw new ConflictException("Email is already registered.");
        }

        _logger.LogInformation("User registered: {UserId} {Email}", user.Id, user.Email);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // same message for wrong email and wrong password (do not reveal which one)
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        _logger.LogInformation("User logged in: {UserId}", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct);

        if (stored is null || !stored.IsActive || stored.User is null)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        // rotation: old token is revoked, a new one is issued
        stored.IsRevoked = true;
        return await IssueTokensAsync(stored.User, ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _tokens.CreateAccessToken(user);

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _tokens.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct); // also saves the revoked old token (rotation)

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refresh.Token,
            AccessTokenExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString()
            }
        };
    }
}