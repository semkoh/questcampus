using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuestCampus.Auth.Api.Data;

namespace QuestCampus.Auth.Api.Services;

public sealed record UserDto(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserDto User);

/// <summary>Émission des jetons d'accès (JWT courts) et des refresh tokens (longs, rotatifs, révocables).</summary>
public sealed class TokenService(
    AuthDbContext db,
    UserManager<AppUser> users,
    IOptions<JwtOptions> options,
    TimeProvider clock,
    ILogger<TokenService> logger)
{
    private readonly JwtOptions _jwt = options.Value;

    public async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var response = await CreateSessionAsync(user);
        await db.SaveChangesAsync(ct);
        return response;
    }

    /// <summary>Prépare une nouvelle paire de jetons (le refresh token est ajouté au contexte, sans sauvegarde).</summary>
    private async Task<AuthResponse> CreateSessionAsync(AppUser user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var roles = (await users.GetRolesAsync(user)).ToList();
        var (refreshToken, refreshHash) = CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwt.RefreshTokenDays)
        });

        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);
        return new AuthResponse(
            CreateAccessToken(user, roles, now, accessExpires), accessExpires,
            refreshToken, now.AddDays(_jwt.RefreshTokenDays),
            new UserDto(user.Id, user.Email ?? string.Empty, user.DisplayName, roles));
    }

    /// <summary>
    /// Échange un refresh token contre une nouvelle paire. Un jeton déjà utilisé et présenté à nouveau
    /// signale un vol probable : toutes les sessions de l'utilisateur sont alors révoquées.
    /// </summary>
    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
            return null;

        if (stored.RevokedAtUtc is not null && stored.ReplacedByTokenHash is not null)
        {
            logger.LogWarning("Réutilisation d'un refresh token détectée pour l'utilisateur {UserId}.", stored.UserId);
            await RevokeAllAsync(stored.UserId, now, ct);
            return null;
        }

        if (!stored.IsActive(now))
            return null;

        var user = await users.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return null;

        // Rotation atomique : l'ancien jeton est révoqué dans la même transaction que la création du nouveau.
        var response = await CreateSessionAsync(user);
        stored.RevokedAtUtc = now;
        stored.ReplacedByTokenHash = Hash(response.RefreshToken);
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAtUtc: null })
        {
            stored.RevokedAtUtc = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var token in active)
            token.RevokedAtUtc = now;
        await db.SaveChangesAsync(ct);
    }

    private string CreateAccessToken(AppUser user, IEnumerable<string> roles, DateTime now, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static (string Token, string Hash) CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return (token, Hash(token));
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
