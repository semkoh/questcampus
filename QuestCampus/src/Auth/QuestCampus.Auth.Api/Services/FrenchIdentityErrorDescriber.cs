using Microsoft.AspNetCore.Identity;

namespace QuestCampus.Auth.Api.Services;

/// <summary>Messages d'erreur Identity en français (ceux qu'un utilisateur peut rencontrer à l'inscription).</summary>
public sealed class FrenchIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = $"L'adresse {email} est déjà utilisée." };
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"L'adresse {userName} est déjà utilisée." };
    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Adresse e-mail invalide." };
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Le mot de passe doit contenir au moins {length} caractères." };
    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Le mot de passe doit contenir un chiffre." };
    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Le mot de passe doit contenir une minuscule." };
    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Le mot de passe doit contenir une majuscule." };
    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Le mot de passe doit contenir un caractère spécial." };
}
