using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace QuestCampus.Bff.Auth;

public static class JwtAuthentication
{
    /// <summary>
    /// Le BFF est la porte d'entrée : il valide le JWT (signature, émetteur, audience, expiration)
    /// avant de router quoi que ce soit vers l'API Métier.
    /// </summary>
    public static IServiceCollection AddBffAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var signingKey = config["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey doit contenir au moins 32 caractères.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    // Navigateur : le jeton arrive par cookie HttpOnly (y compris pour la connexion SignalR).
                    OnMessageReceived = context =>
                    {
                        context.Token = AuthCookies.GetAccessToken(context.HttpContext);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
