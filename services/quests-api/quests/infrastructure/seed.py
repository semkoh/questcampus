"""Quête de démonstration « Rallye du Marais » + une équipe rivale, pour que la démo ait du contenu."""
import logging

from sqlalchemy import select

from ..domain.entities import Attempt, Quest, Team
from ..domain.geo import GeoPoint
from .config import get_settings
from .database import SessionLocal
from .orm import QuestRow, attempt_to_row, quest_to_row, team_to_row
from .repositories import SystemClock

logger = logging.getLogger("quests")


def seed_demo_data() -> None:
    settings = get_settings()
    if not settings.seed_demo_data:
        return
    with SessionLocal() as session:
        if session.scalar(select(QuestRow.id).limit(1)):
            return

        now = SystemClock().now()
        quest = Quest.create(settings.seed_organizer_id, "Rallye du Marais",
                             "Quatre énigmes autour de l'Hôtel de Ville. Rendez-vous sur place, trouvez la réponse "
                             "et validez-la depuis votre téléphone !", now)
        first = quest.add_checkpoint(
            "Le parvis", "Maison commune des Parisiens depuis le XIVe siècle, je fais face à la Seine. Quel est mon nom ?",
            GeoPoint(48.85646, 2.35242), "Hôtel de Ville", 100)
        quest.add_checkpoint(
            "La tour solitaire", "Seul vestige d'une église détruite à la Révolution, je marquais le départ des "
            "pèlerins vers Compostelle. Quel saint porte mon nom ?",
            GeoPoint(48.85797, 2.34888), "Saint-Jacques", 150)
        quest.add_checkpoint(
            "Les sculptures qui dansent", "Seize sculptures colorées s'agitent dans mon bassin. Elles rendent hommage "
            "à un compositeur russe : lequel ?", GeoPoint(48.85928, 2.35139), "Stravinsky", 200)
        quest.add_checkpoint(
            "Le musée à l'envers", "Mes tuyaux sont à l'extérieur et je porte le nom d'un président de la "
            "République. Lequel ?", GeoPoint(48.86061, 2.35225), "Pompidou", 250)
        quest.publish()

        rival = Team.create(quest.id, "Les Éclaireurs", "ECLR42", settings.seed_rival_player_id, now)
        result = first.evaluate("hotel de ville", first.location)
        attempt = Attempt.record(rival.id, first.id, settings.seed_rival_player_id, "hotel de ville",
                                 first.location, result, now)

        session.add(quest_to_row(quest))
        session.flush()
        session.add(team_to_row(rival))
        session.flush()
        session.add(attempt_to_row(attempt))
        session.commit()
        logger.info("Données de démonstration insérées (quête « %s »).", quest.title)
