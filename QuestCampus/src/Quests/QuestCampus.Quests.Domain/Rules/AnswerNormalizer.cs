using System.Globalization;
using System.Text;

namespace QuestCampus.Quests.Domain.Rules;

/// <summary>
/// Normalise une réponse pour une comparaison tolérante :
/// casse, accents, espaces superflus et ponctuation sont ignorés ("  Éiffel-Tower ! " == "eiffel tower").
/// </summary>
public static class AnswerNormalizer
{
    public static string Normalize(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return string.Empty;

        var decomposed = answer.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue; // accent détaché de sa lettre (é = e + ´)

            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0)
                    builder.Append(' ');
                builder.Append(ch);
                pendingSpace = false;
            }
            else
            {
                // espaces, tirets, apostrophes, ponctuation => un seul séparateur
                pendingSpace = true;
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool AreEquivalent(string? expected, string? submitted) =>
        Normalize(expected) is { Length: > 0 } normalized
        && string.Equals(normalized, Normalize(submitted), StringComparison.Ordinal);
}
