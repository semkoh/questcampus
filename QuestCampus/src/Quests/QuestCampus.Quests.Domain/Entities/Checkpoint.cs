using QuestCampus.Quests.Domain.Common;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Domain.Entities;

/// <summary>Étape d'une quête : une énigme à résoudre à un endroit précis.</summary>
public sealed class Checkpoint
{
    public const int TitleMaxLength = 120;
    public const int RiddleMaxLength = 1000;
    public const int AnswerMaxLength = 200;
    public const int MaxPoints = 1000;

    public Guid Id { get; private set; }
    public Guid QuestId { get; private set; }
    public int Order { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Riddle { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string Answer { get; private set; } = string.Empty;
    public int Points { get; private set; }

    public GeoPoint Location => new(Latitude, Longitude);

    private Checkpoint() { } // EF Core

    internal static Checkpoint Create(Guid questId, int order, string title, string riddle,
        GeoPoint location, string answer, int points)
    {
        if (points is < 1 or > MaxPoints)
            throw new DomainException($"Les points doivent être compris entre 1 et {MaxPoints}.");
        if (AnswerNormalizer.Normalize(answer).Length == 0)
            throw new DomainException("La réponse attendue doit contenir au moins une lettre ou un chiffre.");

        return new Checkpoint
        {
            Id = Guid.NewGuid(),
            QuestId = questId,
            Order = order,
            Title = Quest.RequireText(title, TitleMaxLength, "Le titre du checkpoint"),
            Riddle = Quest.RequireText(riddle, RiddleMaxLength, "L'énigme"),
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Answer = Quest.RequireText(answer, AnswerMaxLength, "La réponse"),
            Points = points
        };
    }

    /// <summary>Applique la règle métier de validation à une tentative.</summary>
    public CheckpointValidationResult Evaluate(string submittedAnswer, GeoPoint playerPosition) =>
        CheckpointValidationRule.Evaluate(Location, Answer, playerPosition, submittedAnswer);
}
