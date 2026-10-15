import uuid

import jwt
from fastapi import APIRouter, Depends, Header, Response
from sqlalchemy.orm import Session

from . import security
from .config import Settings, get_settings
from .database import get_session
from .errors import ApiError
from .schemas import AuthResponse, LoginRequest, RefreshRequest, RegisterRequest, UserOut
from .service import AuthService

router = APIRouter(prefix="/api/auth")


def get_service(session: Session = Depends(get_session), settings: Settings = Depends(get_settings)) -> AuthService:
    return AuthService(session, settings)


def current_user_id(authorization: str = Header(default=""), settings: Settings = Depends(get_settings)) -> uuid.UUID:
    """Ce service valide aussi ses propres JWT pour protéger GET /api/auth/me."""
    if not authorization.lower().startswith("bearer "):
        raise ApiError(401, "Non authentifié", "Jeton manquant.")
    try:
        claims = security.decode_access_token(settings, authorization[7:].strip())
        return uuid.UUID(claims["sub"])
    except (jwt.PyJWTError, ValueError):
        raise ApiError(401, "Non authentifié", "Jeton invalide ou expiré.") from None


@router.post("/register", status_code=201, response_model=UserOut)
def register(body: RegisterRequest, service: AuthService = Depends(get_service)):
    return service.register(body.email, body.password, body.display_name)


@router.post("/login", response_model=AuthResponse)
def login(body: LoginRequest, service: AuthService = Depends(get_service)):
    return service.login(body.email, body.password)


@router.post("/refresh", response_model=AuthResponse)
def refresh(body: RefreshRequest, service: AuthService = Depends(get_service)):
    return service.refresh(body.refresh_token)


@router.post("/logout", status_code=204)
def logout(body: RefreshRequest, service: AuthService = Depends(get_service)):
    service.logout(body.refresh_token)
    return Response(status_code=204)


@router.get("/me", response_model=UserOut)
def me(user_id: uuid.UUID = Depends(current_user_id), service: AuthService = Depends(get_service)):
    return service.get_user(user_id)
