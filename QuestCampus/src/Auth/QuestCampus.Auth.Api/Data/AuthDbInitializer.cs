using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace QuestCampus.Auth.Api.Data;

/// <summary>Crée le schéma au démarrage (avec attente de PostgreSQL) puis les rôles et comptes de démonstration.</summary>
public sealed class AuthDbInitializer(
    AuthDbContext db,
    UserManager<AppUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    TimeProvider clock,
    ILogger<AuthDbInitializer> logger)
{
    private const int MaxRetries = 30;

    // Les GUID sont fixes : le service Métier les connaît pour sa propre quête de démonstration.
    private static readonly (Guid Id, string Email, string Password, string DisplayName, string Role)[] DemoUsers =
    [
        (Guid.Parse("a1000000-0000-0000-0000-000000000001"), "organisateur@questcampus.fr", "Orga2026!", "Mme Martin", Roles.Organizer),
        (Guid.Parse("b1000000-0000-0000-0000-000000000001"), "joueur1@questcampus.fr", "Joueur2026!", "Alice", Roles.Player),
        (Guid.Parse("b1000000-0000-0000-0000-000000000002"), "joueur2@questcampus.fr", "Joueur2026!", "Bilal", Roles.Player),
        (Guid.Parse("b1000000-0000-0000-0000-000000000003"), "joueur3@questcampus.fr", "Joueur2026!", "Chloé", Roles.Player)
    ];

    public async Task InitializeAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < MaxRetries && ex is not OperationCanceledException)
            {
                logger.LogWarning("Base auth_db indisponible (essai {Attempt}/{Max}) : {Message}", attempt, MaxRetries, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() });
        }

        if (await db.Users.AnyAsync(ct))
            return;

        foreach (var demo in DemoUsers)
        {
            var user = new AppUser
            {
                Id = demo.Id,
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                DisplayName = demo.DisplayName,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime
            };
            var created = await users.CreateAsync(user, demo.Password);
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, demo.Role);
        }

        logger.LogInformation("{Count} comptes de démonstration créés.", DemoUsers.Length);
    }
}
