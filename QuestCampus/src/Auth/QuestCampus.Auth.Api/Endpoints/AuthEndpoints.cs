using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using QuestCampus.Auth.Api.Data;
using QuestCampus.Auth.Api.Services;

namespace QuestCampus.Auth.Api.Endpoints;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // Inscription publique : toujours en tant que Joueur. Les organisateurs sont créés par l'administration.
        group.MapPost("/register", async (RegisterRequest request, UserManager<AppUser> users, TimeProvider clock) =>
        {
            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            if (displayName.Length is 0 or > AppUser.DisplayNameMaxLength)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["displayName"] = [$"Le pseudo est obligatoire ({AppUser.DisplayNameMaxLength} caractères max)."]
                });

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = request.Email?.Trim(),
                UserName = request.Email?.Trim(),
                DisplayName = displayName,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime
            };

            var result = await users.CreateAsync(user, request.Password ?? string.Empty);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors
                    .GroupBy(e => e.Code.Contains("Password") ? "password" : "email")
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

            await users.AddToRoleAsync(user, Roles.Player);
            return Results.Created("/api/auth/me",
                new UserDto(user.Id, user.Email!, user.DisplayName, [Roles.Player]));
        });

        group.MapPost("/login", async (LoginRequest request, UserManager<AppUser> users, TokenService tokens, CancellationToken ct) =>
        {
            var user = await users.FindByEmailAsync(request.Email?.Trim() ?? string.Empty);
            if (user is null)
                return InvalidCredentials();

            if (await users.IsLockedOutAsync(user))
                return Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Compte verrouillé",
                    detail: "Trop de tentatives échouées. Réessayez dans quelques minutes.");

            if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            {
                await users.AccessFailedAsync(user); // compte les échecs → verrouillage temporaire
                return InvalidCredentials();
            }

            await users.ResetAccessFailedCountAsync(user);
            return Results.Ok(await tokens.IssueAsync(user, ct));
        });

        group.MapPost("/refresh", async (RefreshRequest request, TokenService tokens, CancellationToken ct) =>
        {
            var response = string.IsNullOrWhiteSpace(request.RefreshToken)
                ? null
                : await tokens.RefreshAsync(request.RefreshToken, ct);
            return response is null
                ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session expirée",
                    detail: "Veuillez vous reconnecter.")
                : Results.Ok(response);
        });

        group.MapPost("/logout", async (RefreshRequest request, TokenService tokens, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
                await tokens.RevokeAsync(request.RefreshToken, ct);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, UserManager<AppUser> users) =>
        {
            var user = await users.FindByIdAsync(principal.FindFirstValue("sub") ?? string.Empty);
            if (user is null)
                return Results.NotFound();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new UserDto(user.Id, user.Email ?? string.Empty, user.DisplayName, roles.ToList()));
        }).RequireAuthorization();

        return app;
    }

    // Message volontairement identique pour "email inconnu" et "mauvais mot de passe" (pas d'énumération de comptes).
    private static IResult InvalidCredentials() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Identifiants invalides",
            detail: "Email ou mot de passe incorrect.");
}
