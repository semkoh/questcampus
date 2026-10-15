"""
L'API Métier revalide le JWT transmis par le BFF (défense en profondeur) :
même en atteignant ce service sur le réseau interne, il faudrait un jeton signé par le service Auth.
"""
import uuid

import jwt
from fastapi import Depends, Header

from ..application.errors import ForbiddenError
from ..application.ports import UserContext
from ..infrastructure.config import Settings, get_settings
from .errors import UnauthorizedError

ROLE_ORGANIZER = "Organisateur"
ROLE_PLAYER = "Joueur"


def current_user(authorization: str = Header(default=""), settings: Settings = Depends(get_settings)) -> UserContext:
    if not authorization.lower().startswith("bearer "):
        raise UnauthorizedError("Jeton manquant.")
    try:
        claims = jwt.decode(authorization[7:].strip(), settings.jwt_signing_key, algorithms=["HS256"],
                            audience=settings.jwt_audience, issuer=settings.jwt_issuer, leeway=30,
                            options={"require": ["exp", "sub", "iss", "aud"]})
        roles = claims.get("role", [])
        roles = [roles] if isinstance(roles, str) else roles
        return UserContext(user_id=uuid.UUID(claims["sub"]), is_organizer=ROLE_ORGANIZER in roles)
    except (jwt.PyJWTError, ValueError):
        raise UnauthorizedError("Jeton invalide ou expiré.") from None


def require_organizer(user: UserContext = Depends(current_user)) -> UserContext:
    if not user.is_organizer:
        raise ForbiddenError("Action réservée aux organisateurs.")
    return user


def require_player(user: UserContext = Depends(current_user)) -> UserContext:
    if user.is_organizer:
        raise ForbiddenError("Action réservée aux joueurs.")
    return user
