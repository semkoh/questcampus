using System.Security.Cryptography;
using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Domain.Entities;

namespace QuestCampus.Quests.Infrastructure.Services;

/// <summary>
/// Code d'invitation de 6 caractères, tiré avec un générateur cryptographique.
/// Alphabet sans caractères ambigus (0/O, 1/I/L) pour être dicté facilement : 31^6 ≈ 887 millions de codes.
/// </summary>
internal sealed class InviteCodeGenerator : IInviteCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public string Generate() => RandomNumberGenerator.GetString(Alphabet, Team.InviteCodeLength);
}
