using System.Security.Claims;
using System.Text.Json;
using QuestCampus.Bff.Auth;
using QuestCampus.Bff.Clients;

namespace QuestCampus.Bff.Endpoints;

/// <summary>Façade d'authentification : le BFF parle au service Auth et pose les cookies HttpOnly.</summary>
public static class BffAuthEndpoints
{
    public static IEndpointRouteBuilder MapBffAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff/auth");

        group.MapPost("/login", async (JsonElement credentials, AuthApiClient auth, HttpContext context, CancellationToken ct) =>
        {
            var response = await auth.PostAsync("/api/auth/login", null, credentials, ct);
            return response.IsSuccess ? Results.Ok(AuthCookies.Write(context, response.Json())) : response.ToResult();
        });

        // Inscription puis connexion automatique.
        group.MapPost("/register", async (JsonElement form, AuthApiClient auth, HttpContext context, CancellationToken ct) =>
        {
            var created = await auth.PostAsync("/api/auth/register", null, form, ct);
            if (!created.IsSuccess)
                return created.ToResult();

            // Le formulaire contient déjà email + mot de passe (le champ displayName est ignoré par /login).
            var login = await auth.PostAsync("/api/auth/login", null, form, ct);
            return login.IsSuccess ? Results.Ok(AuthCookies.Write(context, login.Json())) : login.ToResult();
        });

        group.MapPost("/refresh", async (AuthApiClient auth, HttpContext context, CancellationToken ct) =>
        {
            var refreshToken = AuthCookies.GetRefreshToken(context);
            if (refreshToken is null)
                return Results.Unauthorized();

            var response = await auth.PostAsync("/api/auth/refresh", null, new { refreshToken }, ct);
            if (response.IsSuccess)
                return Results.Ok(AuthCookies.Write(context, response.Json()));

            AuthCookies.Clear(context);
            return response.ToResult();
        });

        group.MapPost("/logout", async (AuthApiClient auth, HttpContext context, CancellationToken ct) =>
        {
            var refreshToken = AuthCookies.GetRefreshToken(context);
            if (refreshToken is not null)
                await auth.PostAsync("/api/auth/logout", null, new { refreshToken }, ct);
            AuthCookies.Clear(context);
            return Results.NoContent();
        });

        // Profil lu directement dans le JWT validé : aucun appel réseau.
        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(UserFromClaims(user)))
            .RequireAuthorization();

        return app;
    }

    public static object UserFromClaims(ClaimsPrincipal user) => new
    {
        id = user.FindFirstValue("sub"),
        email = user.FindFirstValue("email"),
        displayName = user.FindFirstValue("name"),
        roles = user.FindAll("role").Select(c => c.Value).ToArray()
    };
}
