"""Tests unitaires de la sécurité : mots de passe, JWT, refresh tokens."""
import uuid
from datetime import UTC, datetime, timedelta

import jwt
import pytest

from app import security
from app.config import Settings

SETTINGS = Settings(jwt_signing_key="test-signing-key-with-more-than-32-characters")
USER_ID = uuid.uuid4()
NOW = datetime.now(UTC)


def test_password_is_hashed_and_verified():
    hashed = security.hash_password("Joueur2026!")
    assert hashed != "Joueur2026!"
    assert security.verify_password(hashed, "Joueur2026!")
    assert not security.verify_password(hashed, "mauvais")


@pytest.mark.parametrize("password, expected_problems", [
    ("Joueur2026!", 0),
    ("court1!", 2),          # trop court + pas de majuscule
    ("joueur2026!", 1),      # pas de majuscule
    ("JOUEUR2026!", 1),      # pas de minuscule
    ("JoueurSansChiffre!", 1),
    ("Joueur2026", 1),       # pas de caractère spécial
])
def test_password_policy(password, expected_problems):
    assert len(security.password_problems(password)) == expected_problems


def test_access_token_contains_identity_and_roles():
    token, expires = security.create_access_token(SETTINGS, USER_ID, "a@b.fr", "Alice", ["Joueur"], NOW)
    claims = security.decode_access_token(SETTINGS, token)
    assert claims["sub"] == str(USER_ID)
    assert claims["role"] == ["Joueur"]
    assert claims["name"] == "Alice"
    assert expires - NOW == timedelta(minutes=15)


def test_expired_token_is_rejected():
    token, _ = security.create_access_token(SETTINGS, USER_ID, "a@b.fr", "Alice", ["Joueur"], NOW - timedelta(hours=1))
    with pytest.raises(jwt.ExpiredSignatureError):
        security.decode_access_token(SETTINGS, token)


def test_token_signed_with_another_key_is_rejected():
    other = Settings(jwt_signing_key="another-signing-key-with-more-than-32-chars")
    token, _ = security.create_access_token(other, USER_ID, "a@b.fr", "Alice", ["Joueur"], NOW)
    with pytest.raises(jwt.InvalidSignatureError):
        security.decode_access_token(SETTINGS, token)


def test_token_for_another_audience_is_rejected():
    other = Settings(jwt_signing_key=SETTINGS.jwt_signing_key, jwt_audience="autre-application")
    token, _ = security.create_access_token(other, USER_ID, "a@b.fr", "Alice", ["Joueur"], NOW)
    with pytest.raises(jwt.InvalidAudienceError):
        security.decode_access_token(SETTINGS, token)


def test_refresh_token_is_random_and_only_its_hash_is_kept():
    token1, hash1 = security.new_refresh_token()
    token2, _ = security.new_refresh_token()
    assert token1 != token2
    assert len(hash1) == 64 and token1 not in hash1
    assert security.hash_token(token1) == hash1
