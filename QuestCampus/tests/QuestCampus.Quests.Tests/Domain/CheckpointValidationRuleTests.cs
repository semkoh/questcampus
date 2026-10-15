using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Tests.Domain;

/// <summary>Règle métier clé : bonne réponse ET distance ≤ 30 m.</summary>
public class CheckpointValidationRuleTests
{
    private static readonly GeoPoint Checkpoint = new(48.85646, 2.35242);
    private const string Expected = "Hôtel de Ville";

    // 1 m vers le nord ≈ 0,000008993° de latitude.
    private static GeoPoint NorthOfCheckpoint(double meters) =>
        new(Checkpoint.Latitude + meters / 111_195.0, Checkpoint.Longitude);

    [Fact]
    public void Correct_answer_on_the_spot_validates_the_checkpoint()
    {
        var result = CheckpointValidationRule.Evaluate(Checkpoint, Expected, Checkpoint, "Hôtel de Ville");

        Assert.True(result.IsAnswerCorrect);
        Assert.True(result.IsWithinRadius);
        Assert.True(result.IsValidated);
        Assert.Equal(0, result.DistanceMeters);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(29.5)]
    public void Correct_answer_within_30_meters_validates(double meters)
    {
        var result = CheckpointValidationRule.Evaluate(Checkpoint, Expected, NorthOfCheckpoint(meters), Expected);

        Assert.True(result.IsValidated);
        Assert.InRange(result.DistanceMeters, meters - 0.2, meters + 0.2);
    }

    [Theory]
    [InlineData(30.6)]
    [InlineData(50)]
    [InlineData(1000)]
    public void Correct_answer_beyond_30_meters_is_refused(double meters)
    {
        var result = CheckpointValidationRule.Evaluate(Checkpoint, Expected, NorthOfCheckpoint(meters), Expected);

        Assert.True(result.IsAnswerCorrect);
        Assert.False(result.IsWithinRadius);
        Assert.False(result.IsValidated);
    }

    [Fact]
    public void Wrong_answer_on_the_spot_is_refused()
    {
        var result = CheckpointValidationRule.Evaluate(Checkpoint, Expected, Checkpoint, "Tour Eiffel");

        Assert.False(result.IsAnswerCorrect);
        Assert.True(result.IsWithinRadius);
        Assert.False(result.IsValidated);
    }

    [Theory]
    [InlineData("hotel de ville")]
    [InlineData("  HÔTEL   DE   VILLE  ")]
    [InlineData("Hotel-de-Ville !")]
    [InlineData("hôtel de ville.")]
    public void Answer_comparison_ignores_case_accents_spaces_and_punctuation(string submitted)
    {
        Assert.True(CheckpointValidationRule.Evaluate(Checkpoint, Expected, Checkpoint, submitted).IsValidated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hotel")]
    [InlineData("hotel de villes")]
    public void Partial_or_empty_answers_are_refused(string submitted)
    {
        Assert.False(CheckpointValidationRule.Evaluate(Checkpoint, Expected, Checkpoint, submitted).IsAnswerCorrect);
    }

    [Fact]
    public void Validation_radius_is_30_meters()
    {
        Assert.Equal(30.0, CheckpointValidationRule.ValidationRadiusMeters);
    }
}
