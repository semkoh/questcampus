using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QuestCampus.Quests.Application.Common;

namespace QuestCampus.Quests.Api.Infrastructure;

public static class Roles
{
    public const string Organizer = "Organisateur";
    public const string Player = "Joueur";
}

public static class JwtAuthentication
{
    /// <summary>
    /// L'API Métier revalide le JWT transmis par le BFF (défense en profondeur) :
    /// même si quelqu'un atteignait ce service sur le réseau interne, il lui faudrait un jeton signé.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var signingKey = config["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey doit contenir au moins 32 caractères.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // on garde les noms courts "sub" et "role"
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Roles.Organizer, p => p.RequireRole(Roles.Organizer))
            .AddPolicy(Roles.Player, p => p.RequireRole(Roles.Player));

        return services;
    }

    /// <summary>Construit le contexte utilisateur de la couche Application à partir des claims du JWT.</summary>
    public static UserContext ToUserContext(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Jeton sans identifiant utilisateur valide.");
        return new UserContext(userId, principal.IsInRole(Roles.Organizer));
    }
}
