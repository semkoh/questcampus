"""Erreurs renvoyées au format ProblemDetails (RFC 9457) : {status, title, detail} ou {errors}."""
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse


class ApiError(Exception):
    def __init__(self, status: int, title: str, detail: str):
        super().__init__(detail)
        self.status, self.title, self.detail = status, title, detail


class ValidationFailed(Exception):
    def __init__(self, errors: dict[str, list[str]]):
        super().__init__("Validation")
        self.errors = errors


def problem(status: int, title: str, detail: str | None = None, errors: dict | None = None) -> JSONResponse:
    body: dict = {"status": status, "title": title}
    if detail:
        body["detail"] = detail
    if errors:
        body["errors"] = errors
    return JSONResponse(body, status_code=status, media_type="application/problem+json")


FIELD_MESSAGES = {
    "email": "Adresse e-mail invalide.",
    "displayName": "Le pseudo est obligatoire (60 caractères max).",
    "password": "Mot de passe invalide.",
}


def register_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(ApiError)
    async def _api_error(_: Request, exc: ApiError):
        return problem(exc.status, exc.title, exc.detail)

    @app.exception_handler(ValidationFailed)
    async def _validation_failed(_: Request, exc: ValidationFailed):
        return problem(400, "Requête invalide", errors=exc.errors)

    @app.exception_handler(RequestValidationError)
    async def _request_validation(_: Request, exc: RequestValidationError):
        errors: dict[str, list[str]] = {}
        for error in exc.errors():
            field = str(error["loc"][-1]) if error["loc"] else "body"
            errors.setdefault(field, []).append(FIELD_MESSAGES.get(field, "Valeur invalide."))
        return problem(400, "Requête invalide", errors=errors)
