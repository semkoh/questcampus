from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Configuration lue dans les variables d'environnement (voir docker-compose.yml)."""

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    database_url: str = "postgresql+psycopg://auth:auth_dev_password@localhost:5433/auth_db"
    jwt_signing_key: str = "dev-only-signing-key-change-me-0123456789abcdef"
    jwt_issuer: str = "questcampus-auth"
    jwt_audience: str = "questcampus"
    access_token_minutes: int = 15
    refresh_token_days: int = 7
    max_failed_logins: int = 5
    lockout_minutes: int = 5
    seed_demo_data: bool = True


@lru_cache
def get_settings() -> Settings:
    settings = Settings()
    if len(settings.jwt_signing_key) < 32:
        raise RuntimeError("JWT_SIGNING_KEY doit contenir au moins 32 caractères.")
    return settings
