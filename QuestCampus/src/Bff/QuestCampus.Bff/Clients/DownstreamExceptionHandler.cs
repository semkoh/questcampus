using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace QuestCampus.Bff.Clients;

/// <summary>Un service interne injoignable ou trop lent devient une erreur 503 explicite pour le front.</summary>
internal sealed class DownstreamExceptionHandler(IProblemDetailsService problemDetails, ILogger<DownstreamExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var unavailable = exception is HttpRequestException
                          || (exception is TaskCanceledException && !context.RequestAborted.IsCancellationRequested);
        if (!unavailable)
            return false; // laissé au gestionnaire par défaut (500)

        logger.LogWarning("Service interne indisponible pour {Path} : {Message}", context.Request.Path, exception.Message);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service indisponible",
                Detail = "Un service interne ne répond pas. Réessayez dans quelques instants."
            }
        });
    }
}
