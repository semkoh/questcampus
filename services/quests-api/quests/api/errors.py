"""Traduction des exceptions métier en réponses HTTP normalisées (ProblemDetails, RFC 9457)."""
import logging

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from ..application.errors import ConflictError, ForbiddenError, NotFoundError
from ..domain.errors import DomainError

logger = logging.getLogger("quests")


class UnauthorizedError(Exception):
    pass


def problem(status: int, title: str, detail: str | None = None, errors: dict | None = None) -> JSONResponse:
    body: dict = {"status": status, "title": title}
    if detail:
        body["detail"] = detail
    if errors:
        body["errors"] = errors
    return JSONResponse(body, status_code=status, media_type="application/problem+json")


MAPPING = {
    DomainError: (400, "Requête invalide"),
    UnauthorizedError: (401, "Non authentifié"),
    ForbiddenError: (403, "Accès refusé"),
    NotFoundError: (404, "Introuvable"),
    ConflictError: (409, "Conflit"),
}


def register_error_handlers(app: FastAPI) -> None:
    for exc_type, (status, title) in MAPPING.items():
        def handler(_: Request, exc: Exception, status=status, title=title):
            return problem(status, title, str(exc))
        app.add_exception_handler(exc_type, handler)

    @app.exception_handler(RequestValidationError)
    async def _validation(_: Request, exc: RequestValidationError):
        errors: dict[str, list[str]] = {}
        for error in exc.errors():
            field = str(error["loc"][-1]) if error["loc"] else "body"
            errors.setdefault(field, []).append("Valeur manquante ou invalide.")
        return problem(400, "Requête invalide", errors=errors)

    @app.exception_handler(Exception)
    async def _unexpected(request: Request, exc: Exception):
        # Le détail technique d'une erreur 500 n'est jamais exposé au client.
        logger.exception("Erreur non gérée sur %s", request.url.path)
        return problem(500, "Erreur interne", "Une erreur inattendue est survenue.")
