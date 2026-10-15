"""Règle métier clé : bonne réponse ET distance ≤ 30 m."""
import pytest

from quests.domain.geo import GeoPoint
from quests.domain.validation import VALIDATION_RADIUS_METERS, evaluate_attempt

CHECKPOINT = GeoPoint(48.85646, 2.35242)
EXPECTED = "Hôtel de Ville"


def north_of_checkpoint(meters: float) -> GeoPoint:
    # 1 m vers le nord ≈ 1 / 111 195 degré de latitude
    return GeoPoint(CHECKPOINT.latitude + meters / 111_195.0, CHECKPOINT.longitude)


def test_correct_answer_on_the_spot_validates():
    result = evaluate_attempt(CHECKPOINT, EXPECTED, CHECKPOINT, "Hôtel de Ville")
    assert result.is_answer_correct and result.is_within_radius and result.is_validated
    assert result.distance_meters == 0


@pytest.mark.parametrize("meters", [10, 25, 29.5])
def test_correct_answer_within_30_meters_validates(meters):
    result = evaluate_attempt(CHECKPOINT, EXPECTED, north_of_checkpoint(meters), EXPECTED)
    assert result.is_validated
    assert result.distance_meters == pytest.approx(meters, abs=0.2)


@pytest.mark.parametrize("meters", [30.6, 50, 1000])
def test_correct_answer_beyond_30_meters_is_refused(meters):
    result = evaluate_attempt(CHECKPOINT, EXPECTED, north_of_checkpoint(meters), EXPECTED)
    assert result.is_answer_correct
    assert not result.is_within_radius
    assert not result.is_validated


def test_wrong_answer_on_the_spot_is_refused():
    result = evaluate_attempt(CHECKPOINT, EXPECTED, CHECKPOINT, "Tour Eiffel")
    assert not result.is_answer_correct and result.is_within_radius and not result.is_validated


@pytest.mark.parametrize("submitted", ["hotel de ville", "  HÔTEL   DE   VILLE  ", "Hotel-de-Ville !", "hôtel de ville."])
def test_answer_ignores_case_accents_spaces_and_punctuation(submitted):
    assert evaluate_attempt(CHECKPOINT, EXPECTED, CHECKPOINT, submitted).is_validated


@pytest.mark.parametrize("submitted", ["", "   ", "hotel", "hotel de villes"])
def test_partial_or_empty_answers_are_refused(submitted):
    assert not evaluate_attempt(CHECKPOINT, EXPECTED, CHECKPOINT, submitted).is_answer_correct


def test_validation_radius_is_30_meters():
    assert VALIDATION_RADIUS_METERS == 30.0
