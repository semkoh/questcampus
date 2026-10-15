"""Création du schéma (avec attente de PostgreSQL) puis des rôles et comptes de démonstration."""
import logging
import time
import uuid

from sqlalchemy import select
from sqlalchemy.exc import OperationalError

from . import security
from .config import get_settings
from .database import Base, SessionLocal, engine
from .models import ALL_ROLES, ROLE_ORGANIZER, ROLE_PLAYER, Role, User
from .service import utcnow

logger = logging.getLogger("auth")

# Identifiants fixes : le service Métier les connaît pour sa propre quête de démonstration.
DEMO_USERS = [
    (uuid.UUID("a1000000-0000-0000-0000-000000000001"), "organisateur@questcampus.fr", "Orga2026!", "Mme Martin", ROLE_ORGANIZER),
    (uuid.UUID("b1000000-0000-0000-0000-000000000001"), "joueur1@questcampus.fr", "Joueur2026!", "Alice", ROLE_PLAYER),
    (uuid.UUID("b1000000-0000-0000-0000-000000000002"), "joueur2@questcampus.fr", "Joueur2026!", "Bilal", ROLE_PLAYER),
    (uuid.UUID("b1000000-0000-0000-0000-000000000003"), "joueur3@questcampus.fr", "Joueur2026!", "Chloé", ROLE_PLAYER),
]


def wait_for_database(retries: int = 30, delay: float = 2.0) -> None:
    for attempt in range(1, retries + 1):
        try:
            Base.metadata.create_all(engine)
            return
        except OperationalError as exc:
            if attempt == retries:
                raise
            logger.warning("Base auth_db indisponible (essai %s/%s) : %s", attempt, retries, exc.__class__.__name__)
            time.sleep(delay)


def initialize() -> None:
    wait_for_database()
    with SessionLocal() as session:
        for name in ALL_ROLES:
            if not session.scalar(select(Role).where(Role.name == name)):
                session.add(Role(id=uuid.uuid4(), name=name))
        session.commit()

        if not get_settings().seed_demo_data or session.scalar(select(User).limit(1)):
            return

        roles = {r.name: r for r in session.scalars(select(Role))}
        for user_id, email, password, display_name, role in DEMO_USERS:
            user = User(id=user_id, email=email, password_hash=security.hash_password(password),
                        display_name=display_name, created_at=utcnow(), failed_login_count=0)
            user.roles.append(roles[role])
            session.add(user)
        session.commit()
        logger.info("%s comptes de démonstration créés.", len(DEMO_USERS))
