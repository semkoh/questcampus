using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Infrastructure.Persistence;

/// <summary>Identifiants des comptes de démonstration créés par le service Auth (mêmes GUID des deux côtés).</summary>
public sealed record SeedOptions(Guid OrganizerId, Guid RivalPlayerId);

/// <summary>
/// Crée le schéma au démarrage (en attendant que PostgreSQL soit prêt) puis insère une quête de démonstration.
/// </summary>
public sealed class DatabaseInitializer(QuestsDbContext db, TimeProvider clock, ILogger<DatabaseInitializer> logger)
{
    private const int MaxRetries = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task InitializeAsync(SeedOptions seed, CancellationToken ct)
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
                logger.LogWarning("Base quests_db indisponible (essai {Attempt}/{Max}) : {Message}",
                    attempt, MaxRetries, ex.Message);
                await Task.Delay(RetryDelay, ct);
            }
        }

        if (await db.Quests.AnyAsync(ct))
            return;

        var now = clock.GetUtcNow().UtcDateTime;
        var quest = Quest.Create(seed.OrganizerId, "Rallye du Marais",
            "Quatre énigmes autour de l'Hôtel de Ville. Rendez-vous sur place, trouvez la réponse et validez-la depuis votre téléphone !",
            now);

        var first = quest.AddCheckpoint("Le parvis",
            "Maison commune des Parisiens depuis le XIVe siècle, je fais face à la Seine. Quel est mon nom ?",
            new GeoPoint(48.85646, 2.35242), "Hôtel de Ville", 100);
        quest.AddCheckpoint("La tour solitaire",
            "Seul vestige d'une église détruite à la Révolution, je marquais le départ des pèlerins vers Compostelle. Quel saint porte mon nom ?",
            new GeoPoint(48.85797, 2.34888), "Saint-Jacques", 150);
        quest.AddCheckpoint("Les sculptures qui dansent",
            "Seize sculptures colorées s'agitent dans mon bassin. Elles rendent hommage à un compositeur russe : lequel ?",
            new GeoPoint(48.85928, 2.35139), "Stravinsky", 200);
        quest.AddCheckpoint("Le musée à l'envers",
            "Mes tuyaux sont à l'extérieur et je porte le nom d'un président de la République. Lequel ?",
            new GeoPoint(48.86061, 2.35225), "Pompidou", 250);
        quest.Publish();

        // Équipe rivale déjà en jeu, pour que le classement ne soit pas vide pendant la démo.
        var rival = Team.Create(quest.Id, "Les Éclaireurs", "ECLR42", seed.RivalPlayerId, now);
        var result = first.Evaluate("hotel de ville", first.Location);
        var rivalAttempt = Attempt.Record(rival.Id, first.Id, seed.RivalPlayerId, "hotel de ville",
            first.Location, result, now);

        db.Quests.Add(quest);
        db.Teams.Add(rival);
        db.Attempts.Add(rivalAttempt);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Données de démonstration insérées (quête « {Title} »).", quest.Title);
    }
}
