class NotFoundError(Exception):
    """Ressource introuvable (HTTP 404)."""


class ForbiddenError(Exception):
    """Action interdite pour cet utilisateur (HTTP 403)."""


class ConflictError(Exception):
    """Conflit avec l'état actuel, ex. déjà membre d'une équipe (HTTP 409)."""
