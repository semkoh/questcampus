using Microsoft.AspNetCore.Identity;

namespace QuestCampus.Auth.Api.Data;

/// <summary>Utilisateur Identity (clé Guid, partagée avec le service Métier sous forme de "sub" dans le JWT).</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 60;

    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public static class Roles
{
    public const string Organizer = "Organisateur";
    public const string Player = "Joueur";
    public static readonly string[] All = [Organizer, Player];
}
