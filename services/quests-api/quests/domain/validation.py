from dataclasses import dataclass

from .answers import answers_match
from .geo import GeoPoint, haversine_meters

# Règle métier centrale : le joueur doit se trouver à 30 m au plus du checkpoint.
VALIDATION_RADIUS_METERS = 30.0


@dataclass(frozen=True)
class ValidationResult:
    is_answer_correct: bool
    is_within_radius: bool
    distance_meters: float

    @property
    def is_validated(self) -> bool:
        """Un checkpoint n'est validé que si la réponse est correcte ET la position dans le rayon."""
        return self.is_answer_correct and self.is_within_radius


def evaluate_attempt(checkpoint_location: GeoPoint, expected_answer: str,
                     player_position: GeoPoint, submitted_answer: str) -> ValidationResult:
    distance = haversine_meters(checkpoint_location, player_position)
    return ValidationResult(
        is_answer_correct=answers_match(expected_answer, submitted_answer),
        is_within_radius=distance <= VALIDATION_RADIUS_METERS,
        distance_meters=round(distance, 1),
    )
