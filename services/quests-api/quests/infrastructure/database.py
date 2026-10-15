import logging
import time
from collections.abc import Iterator

from sqlalchemy import create_engine
from sqlalchemy.exc import OperationalError
from sqlalchemy.orm import Session, sessionmaker

from .config import get_settings
from .orm import Base

logger = logging.getLogger("quests")

engine = create_engine(get_settings().database_url, pool_pre_ping=True)
SessionLocal = sessionmaker(bind=engine, expire_on_commit=False)


def get_session() -> Iterator[Session]:
    """Une session SQLAlchemy par requête HTTP."""
    with SessionLocal() as session:
        yield session


def create_schema(retries: int = 30, delay: float = 2.0) -> None:
    """Crée les tables, en attendant que PostgreSQL soit prêt (démarrage Docker)."""
    for attempt in range(1, retries + 1):
        try:
            Base.metadata.create_all(engine)
            return
        except OperationalError as exc:
            if attempt == retries:
                raise
            logger.warning("Base quests_db indisponible (essai %s/%s) : %s", attempt, retries, exc.__class__.__name__)
            time.sleep(delay)
