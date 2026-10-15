using System.Text.Json;

namespace QuestCampus.Bff.Auth;

/// <summary>
/// Les jetons ne sont jamais exposés au JavaScript : ils vivent dans des cookies HttpOnly + SameSite=Strict
/// gérés par le BFF (protection contre le vol de jeton par XSS).
/// </summary>
public static class AuthCookies
{
    public const string AccessCookie = "qc_access";
    public const string RefreshCookie = "qc_refresh";
    private const string RefreshCookiePath = "/bff/auth";

    /// <summary>Jeton d'accès de la requête : en-tête Authorization (outils, tests) sinon cookie (navigateur).</summary>
    public static string? GetAccessToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header["Bearer ".Length..].Trim();
        return context.Request.Cookies.TryGetValue(AccessCookie, out var cookie) ? cookie : null;
    }

    public static string? GetRefreshToken(HttpContext context) =>
        context.Request.Cookies.TryGetValue(RefreshCookie, out var token) ? token : null;

    /// <summary>Écrit les cookies à partir de la réponse JSON du service Auth et renvoie l'objet "user".</summary>
    public static JsonElement Write(HttpContext context, JsonElement authResponse)
    {
        var accessToken = authResponse.GetProperty("accessToken").GetString()!;
        var refreshToken = authResponse.GetProperty("refreshToken").GetString()!;
        var refreshExpires = authResponse.GetProperty("refreshTokenExpiresAtUtc").GetDateTime();

        // Le cookie d'accès vit aussi longtemps que la session : c'est le JWT lui-même qui porte
        // sa courte expiration (15 min) ; au-delà le front appelle /bff/auth/refresh.
        context.Response.Cookies.Append(AccessCookie, accessToken, Options(context, "/", refreshExpires));
        context.Response.Cookies.Append(RefreshCookie, refreshToken, Options(context, RefreshCookiePath, refreshExpires));
        return authResponse.GetProperty("user");
    }

    public static void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(AccessCookie, Options(context, "/", null));
        context.Response.Cookies.Delete(RefreshCookie, Options(context, RefreshCookiePath, null));
    }

    private static CookieOptions Options(HttpContext context, string path, DateTime? expiresUtc) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        Path = path,
        Expires = expiresUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc))
    };
}
