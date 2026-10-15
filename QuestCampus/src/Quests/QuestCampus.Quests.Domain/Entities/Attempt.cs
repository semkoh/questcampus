using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Domain.Entities;

/// <summary>Trace d'une tentative (réussie ou non) : sert à l'audit et au calcul du classement.</summary>
public sealed class Attempt
{
    public const int SubmittedAnswerMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid CheckpointId { get; private set; }
    public Guid UserId { get; private set; }
    public string SubmittedAnswer { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double DistanceMeters { get; private set; }
    public bool IsSuccess { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }

    private Attempt() { } // EF Core

    public static Attempt Record(Guid teamId, Guid checkpointId, Guid userId, string submittedAnswer,
        GeoPoint position, CheckpointValidationResult result, DateTime nowUtc)
    {
        var answer = (submittedAnswer ?? string.Empty).Trim();
        return new Attempt
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            CheckpointId = checkpointId,
            UserId = userId,
            SubmittedAnswer = answer.Length > SubmittedAnswerMaxLength ? answer[..SubmittedAnswerMaxLength] : answer,
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            DistanceMeters = result.DistanceMeters,
            IsSuccess = result.IsValidated,
            SubmittedAtUtc = nowUtc
        };
    }
}
