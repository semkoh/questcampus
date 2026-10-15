import uuid
from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Configuration lue dans les variables d'environnement (voir docker-compose.yml)."""

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    database_url: str = "postgresql+psycopg://quests:quests_dev_password@localhost:5434/quests_db"
    jwt_signing_key: str = "dev-only-signing-key-change-me-0123456789abcdef"
    jwt_issuer: str = "questcampus-auth"
    jwt_audience: str = "questcampus"
    seed_demo_data: bool = True
    # Comptes de démonstration créés par le service Auth (mêmes identifiants des deux côtés).
    seed_organizer_id: uuid.UUID = uuid.UUID("a1000000-0000-0000-0000-000000000001")
    seed_rival_player_id: uuid.UUID = uuid.UUID("b1000000-0000-0000-0000-000000000003")


@lru_cache
def get_settings() -> Settings:
    settings = Settings()
    if len(settings.jwt_signing_key) < 32:
        raise RuntimeError("JWT_SIGNING_KEY doit contenir au moins 32 caractères.")
    return settings
