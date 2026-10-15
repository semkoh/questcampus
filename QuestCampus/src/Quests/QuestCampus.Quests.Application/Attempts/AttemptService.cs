using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Application.Quests;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Application.Attempts;

public sealed record SubmitAttemptRequest(string Answer, double Latitude, double Longitude);

public sealed record AttemptResultDto(
    bool IsValidated,
    bool AlreadyValidated,
    bool IsAnswerCorrect,
    bool IsWithinRadius,
    double DistanceMeters,
    int PointsAwarded,
    string Message);

/// <summary>Cas d'usage principal : un joueur soumet une réponse depuis sa position GPS.</summary>
public sealed class AttemptService(
    IQuestRepository quests,
    ITeamRepository teams,
    IAttemptRepository attempts,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<AttemptResultDto> SubmitAsync(Guid questId, Guid checkpointId,
        SubmitAttemptRequest request, UserContext user, CancellationToken ct)
    {
        if (user.IsOrganizer)
            throw new ForbiddenException("Les organisateurs ne peuvent pas valider de checkpoint.");

        // Pour un joueur, une quête non publiée est invisible (404).
        var quest = await QuestService.GetVisibleQuestAsync(quests, questId, user, ct);

        var checkpoint = quest.Checkpoints.FirstOrDefault(c => c.Id == checkpointId)
                         ?? throw new NotFoundException("Checkpoint introuvable dans cette quête.");

        var team = await teams.GetTeamOfUserForQuestAsync(user.UserId, questId, ct)
                   ?? throw new ConflictException("Rejoignez ou créez une équipe avant de jouer.");

        if (await attempts.HasSuccessAsync(team.Id, checkpoint.Id, ct))
            return new AttemptResultDto(false, true, true, true, 0, 0,
                "Votre équipe a déjà validé ce checkpoint.");

        var position = new GeoPoint(request.Latitude, request.Longitude);
        var result = checkpoint.Evaluate(request.Answer, position);

        attempts.Add(Attempt.Record(team.Id, checkpoint.Id, user.UserId, request.Answer, position,
            result, clock.GetUtcNow().UtcDateTime));
        await unitOfWork.SaveChangesAsync(ct);

        var points = result.IsValidated ? checkpoint.Points : 0;
        return new AttemptResultDto(result.IsValidated, false, result.IsAnswerCorrect, result.IsWithinRadius,
            result.DistanceMeters, points, BuildMessage(result, points));
    }

    private static string BuildMessage(CheckpointValidationResult r, int points) => r switch
    {
        { IsValidated: true } => $"Checkpoint validé ! +{points} points pour votre équipe.",
        { IsAnswerCorrect: true, IsWithinRadius: false } =>
            $"Bonne réponse, mais vous êtes à {r.DistanceMeters:0} m du point : rapprochez-vous à moins de {CheckpointValidationRule.ValidationRadiusMeters:0} m.",
        { IsAnswerCorrect: false, IsWithinRadius: true } => "Vous êtes au bon endroit, mais la réponse est incorrecte.",
        _ => $"Mauvaise réponse et vous êtes à {r.DistanceMeters:0} m du point."
    };
}
