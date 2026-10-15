using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Domain.Common;

namespace QuestCampus.Quests.Api.Infrastructure;

/// <summary>Traduit les exceptions métier en réponses HTTP normalisées (RFC 9457 ProblemDetails).</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Requête invalide"),
            NotFoundException => (StatusCodes.Status404NotFound, "Introuvable"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Accès refusé"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflit"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Non authentifié"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Requête invalide"),
            _ => (StatusCodes.Status500InternalServerError, "Erreur interne")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Erreur non gérée sur {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // On n'expose jamais le détail technique d'une erreur 500.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Une erreur inattendue est survenue."
                    : exception.Message
            }
        });
    }
}
