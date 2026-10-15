"""Distance haversine et objet valeur GeoPoint."""
import pytest

from quests.domain.errors import DomainError
from quests.domain.geo import GeoPoint, haversine_meters


def test_distance_between_identical_points_is_zero():
    point = GeoPoint(48.85646, 2.35242)
    assert haversine_meters(point, point) == pytest.approx(0, abs=1e-6)


def test_one_degree_of_latitude_is_about_111_km():
    assert 111_190 <= haversine_meters(GeoPoint(0, 0), GeoPoint(1, 0)) <= 111_200


def test_paris_to_london_is_about_340_km():
    notre_dame, big_ben = GeoPoint(48.8530, 2.3499), GeoPoint(51.5007, -0.1246)
    assert 338 <= haversine_meters(notre_dame, big_ben) / 1000 <= 345


def test_distance_is_symmetric():
    a, b = GeoPoint(48.85646, 2.35242), GeoPoint(48.85797, 2.34888)
    assert haversine_meters(a, b) == pytest.approx(haversine_meters(b, a))


@pytest.mark.parametrize("lat, lon", [(91, 0), (-91, 0), (0, 181), (0, -181), (float("nan"), 0)])
def test_invalid_coordinates_are_rejected(lat, lon):
    with pytest.raises(DomainError):
        GeoPoint(lat, lon)
