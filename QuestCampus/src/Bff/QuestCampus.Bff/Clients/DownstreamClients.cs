using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuestCampus.Bff.Clients;

/// <summary>Réponse brute d'un service en aval, renvoyée telle quelle au front si besoin.</summary>
public sealed record DownstreamResponse(int StatusCode, string Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public JsonElement Json() => JsonDocument.Parse(Body).RootElement.Clone();

    /// <summary>Retransmet le statut et le corps (ProblemDetails compris) au client.</summary>
    public IResult ToResult() => string.IsNullOrEmpty(Body)
        ? Results.StatusCode(StatusCode)
        : Results.Content(Body, IsSuccess ? "application/json" : "application/problem+json", statusCode: StatusCode);
}

/// <summary>Client HTTP commun : ajoute le JWT de l'utilisateur et capture la réponse.</summary>
public abstract class DownstreamClient(HttpClient http)
{
    public async Task<DownstreamResponse> SendAsync(HttpMethod method, string path, string? accessToken,
        object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await http.SendAsync(request, ct);
        return new DownstreamResponse((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    public Task<DownstreamResponse> GetAsync(string path, string? accessToken, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, path, accessToken, null, ct);

    public Task<DownstreamResponse> PostAsync(string path, string? accessToken, object? body, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, path, accessToken, body, ct);
}

/// <summary>Client typé vers le service Auth (http://auth-api:8080 dans Docker).</summary>
public sealed class AuthApiClient(HttpClient http) : DownstreamClient(http);

/// <summary>Client typé vers l'API Métier (http://quests-api:8080 dans Docker).</summary>
public sealed class QuestsApiClient(HttpClient http) : DownstreamClient(http);
