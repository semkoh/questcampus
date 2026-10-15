"""Hachage des mots de passe (Argon2), politique de mot de passe et jetons."""
import hashlib
import re
import secrets
import uuid
from datetime import datetime, timedelta

import jwt
from argon2 import PasswordHasher
from argon2.exceptions import VerificationError

from .config import Settings

_hasher = PasswordHasher()


def hash_password(password: str) -> str:
    return _hasher.hash(password)


def verify_password(password_hash: str, password: str) -> bool:
    try:
        return _hasher.verify(password_hash, password)
    except VerificationError:
        return False


def password_problems(password: str) -> list[str]:
    """Règles de robustesse : renvoie la liste des règles non respectées (vide = mot de passe accepté)."""
    rules = [
        (len(password) >= 8, "Le mot de passe doit contenir au moins 8 caractères."),
        (re.search(r"[a-z]", password) is not None, "Le mot de passe doit contenir une minuscule."),
        (re.search(r"[A-Z]", password) is not None, "Le mot de passe doit contenir une majuscule."),
        (re.search(r"\d", password) is not None, "Le mot de passe doit contenir un chiffre."),
        (re.search(r"[^A-Za-z0-9]", password) is not None, "Le mot de passe doit contenir un caractère spécial."),
    ]
    return [message for ok, message in rules if not ok]


def create_access_token(settings: Settings, user_id: uuid.UUID, email: str, display_name: str,
                        roles: list[str], now: datetime) -> tuple[str, datetime]:
    """JWT signé HS256. La claim "sub" est le seul lien entre l'utilisateur et le service Métier."""
    expires = now + timedelta(minutes=settings.access_token_minutes)
    claims = {
        "sub": str(user_id),
        "email": email,
        "name": display_name,
        "role": roles,
        "iss": settings.jwt_issuer,
        "aud": settings.jwt_audience,
        "iat": int(now.timestamp()),
        "nbf": int(now.timestamp()),
        "exp": int(expires.timestamp()),
        "jti": str(uuid.uuid4()),
    }
    return jwt.encode(claims, settings.jwt_signing_key, algorithm="HS256"), expires


def decode_access_token(settings: Settings, token: str) -> dict:
    """Vérifie signature, émetteur, audience et expiration (30 s de tolérance d'horloge)."""
    return jwt.decode(
        token,
        settings.jwt_signing_key,
        algorithms=["HS256"],
        audience=settings.jwt_audience,
        issuer=settings.jwt_issuer,
        leeway=30,
        options={"require": ["exp", "sub", "iss", "aud"]},
    )


def new_refresh_token() -> tuple[str, str]:
    """Jeton aléatoire de 64 octets et son empreinte SHA-256 (seule l'empreinte est stockée)."""
    token = secrets.token_urlsafe(64)
    return token, hash_token(token)


def hash_token(token: str) -> str:
    return hashlib.sha256(token.encode("utf-8")).hexdigest()
