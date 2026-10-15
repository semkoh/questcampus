"""Logique d'authentification : inscription, connexion, rotation et révocation des jetons."""
import logging
import uuid
from datetime import UTC, datetime, timedelta

from sqlalchemy import select, update
from sqlalchemy.orm import Session

from . import security
from .config import Settings
from .errors import ApiError, ValidationFailed
from .models import ROLE_PLAYER, RefreshToken, Role, User
from .schemas import AuthResponse, UserOut

logger = logging.getLogger("auth")

INVALID_CREDENTIALS = ApiError(401, "Identifiants invalides", "Email ou mot de passe incorrect.")
SESSION_EXPIRED = ApiError(401, "Session expirée", "Veuillez vous reconnecter.")


def utcnow() -> datetime:
    return datetime.now(UTC)


def to_user_out(user: User) -> UserOut:
    return UserOut(id=user.id, email=user.email, display_name=user.display_name, roles=user.role_names)


class AuthService:
    def __init__(self, session: Session, settings: Settings):
        self.session = session
        self.settings = settings

    # ---------- Inscription ----------
    def register(self, email: str, password: str, display_name: str) -> UserOut:
        """Inscription publique : toujours en tant que Joueur (les organisateurs sont créés par l'administration)."""
        email = email.strip().lower()
        problems = security.password_problems(password)
        if problems:
            raise ValidationFailed({"password": problems})
        if self.session.scalar(select(User).where(User.email == email)):
            raise ValidationFailed({"email": [f"L'adresse {email} est déjà utilisée."]})

        user = User(id=uuid.uuid4(), email=email, password_hash=security.hash_password(password),
                    display_name=display_name.strip(), created_at=utcnow(), failed_login_count=0)
        user.roles.append(self._role(ROLE_PLAYER))
        self.session.add(user)
        self.session.commit()
        return to_user_out(user)

    # ---------- Connexion ----------
    def login(self, email: str, password: str) -> AuthResponse:
        now = utcnow()
        user = self.session.scalar(select(User).where(User.email == email.strip().lower()))
        if user is None:
            # Message identique pour "email inconnu" et "mauvais mot de passe" : pas d'énumération de comptes.
            raise INVALID_CREDENTIALS

        if user.lockout_end and user.lockout_end > now:
            raise ApiError(423, "Compte verrouillé", "Trop de tentatives échouées. Réessayez dans quelques minutes.")

        if not security.verify_password(user.password_hash, password):
            user.failed_login_count += 1
            if user.failed_login_count >= self.settings.max_failed_logins:
                user.lockout_end = now + timedelta(minutes=self.settings.lockout_minutes)
                user.failed_login_count = 0
            self.session.commit()
            raise INVALID_CREDENTIALS

        user.failed_login_count = 0
        user.lockout_end = None
        response = self._create_session(user, now)
        self.session.commit()
        return response

    # ---------- Rafraîchissement ----------
    def refresh(self, refresh_token: str) -> AuthResponse:
        """
        Échange un refresh token contre une nouvelle paire (rotation). Si un jeton déjà échangé est
        présenté à nouveau, il a probablement été volé : toutes les sessions de l'utilisateur sont révoquées.
        """
        now = utcnow()
        stored = self.session.scalar(
            select(RefreshToken).where(RefreshToken.token_hash == security.hash_token(refresh_token)))
        if stored is None:
            raise SESSION_EXPIRED

        if stored.revoked_at is not None and stored.replaced_by_token_hash is not None:
            logger.warning("Réutilisation d'un refresh token détectée pour l'utilisateur %s", stored.user_id)
            self.session.execute(update(RefreshToken)
                                 .where(RefreshToken.user_id == stored.user_id, RefreshToken.revoked_at.is_(None))
                                 .values(revoked_at=now))
            self.session.commit()
            raise SESSION_EXPIRED

        if not stored.is_active(now):
            raise SESSION_EXPIRED

        user = self.session.get(User, stored.user_id)
        if user is None:
            raise SESSION_EXPIRED

        # Rotation atomique : l'ancien jeton est révoqué dans la même transaction que la création du nouveau.
        response = self._create_session(user, now)
        stored.revoked_at = now
        stored.replaced_by_token_hash = security.hash_token(response.refresh_token)
        self.session.commit()
        return response

    def logout(self, refresh_token: str) -> None:
        stored = self.session.scalar(
            select(RefreshToken).where(RefreshToken.token_hash == security.hash_token(refresh_token)))
        if stored is not None and stored.revoked_at is None:
            stored.revoked_at = utcnow()
            self.session.commit()

    def get_user(self, user_id: uuid.UUID) -> UserOut:
        user = self.session.get(User, user_id)
        if user is None:
            raise ApiError(404, "Introuvable", "Utilisateur introuvable.")
        return to_user_out(user)

    # ---------- Interne ----------
    def _create_session(self, user: User, now: datetime) -> AuthResponse:
        access_token, access_expires = security.create_access_token(
            self.settings, user.id, user.email, user.display_name, user.role_names, now)
        refresh_token, refresh_hash = security.new_refresh_token()
        refresh_expires = now + timedelta(days=self.settings.refresh_token_days)
        self.session.add(RefreshToken(id=uuid.uuid4(), user_id=user.id, token_hash=refresh_hash,
                                      created_at=now, expires_at=refresh_expires))
        return AuthResponse(access_token=access_token, access_token_expires_at_utc=access_expires,
                            refresh_token=refresh_token, refresh_token_expires_at_utc=refresh_expires,
                            user=to_user_out(user))

    def _role(self, name: str) -> Role:
        role = self.session.scalar(select(Role).where(Role.name == name))
        if role is None:
            raise RuntimeError(f"Rôle {name} absent : la base n'a pas été initialisée.")
        return role
