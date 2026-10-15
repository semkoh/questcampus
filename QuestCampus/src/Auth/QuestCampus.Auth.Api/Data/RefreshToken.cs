namespace QuestCampus.Auth.Api.Data;

/// <summary>
/// Jeton de rafraîchissement. Seule son empreinte SHA-256 est stockée : une fuite de la base
/// ne permet pas de réutiliser les jetons. Rotation à chaque usage (ReplacedByTokenHash).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
