namespace QuestCampus.Quests.Domain.Rules;

/// <summary>Résultat détaillé de l'évaluation d'une tentative.</summary>
public sealed record CheckpointValidationResult(
    bool IsAnswerCorrect,
    bool IsWithinRadius,
    double DistanceMeters)
{
    /// <summary>Un checkpoint n'est validé que si la réponse est correcte ET la position dans le rayon.</summary>
    public bool IsValidated => IsAnswerCorrect && IsWithinRadius;
}

/// <summary>
/// Règle métier centrale de Quest Campus : une tentative valide un checkpoint
/// si et seulement si la réponse est juste ET le joueur se trouve à 30 m maximum du point.
/// </summary>
public static class CheckpointValidationRule
{
    public const double ValidationRadiusMeters = 30.0;

    public static CheckpointValidationResult Evaluate(
        GeoPoint checkpointLocation,
        string expectedAnswer,
        GeoPoint playerPosition,
        string submittedAnswer)
    {
        var distance = GeoDistance.HaversineMeters(checkpointLocation, playerPosition);

        return new CheckpointValidationResult(
            IsAnswerCorrect: AnswerNormalizer.AreEquivalent(expectedAnswer, submittedAnswer),
            IsWithinRadius: distance <= ValidationRadiusMeters,
            DistanceMeters: Math.Round(distance, 1));
    }
}
