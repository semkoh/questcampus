import math
from dataclasses import dataclass

from .errors import DomainError

# Rayon moyen de la Terre en mètres (valeur IUGG).
EARTH_RADIUS_METERS = 6_371_008.8


@dataclass(frozen=True)
class GeoPoint:
    """Position GPS (WGS 84) en degrés décimaux. Objet valeur immuable et toujours valide."""

    latitude: float
    longitude: float

    def __post_init__(self) -> None:
        if math.isnan(self.latitude) or not -90 <= self.latitude <= 90:
            raise DomainError("La latitude doit être comprise entre -90 et 90.")
        if math.isnan(self.longitude) or not -180 <= self.longitude <= 180:
            raise DomainError("La longitude doit être comprise entre -180 et 180.")


def haversine_meters(a: GeoPoint, b: GeoPoint) -> float:
    """
    Distance à vol d'oiseau sur la sphère terrestre (formule de haversine).
    Stable numériquement pour les petites distances, ce qui est exactement notre cas (rayon de 30 m).
    """
    lat1, lat2 = math.radians(a.latitude), math.radians(b.latitude)
    d_lat = math.radians(b.latitude - a.latitude)
    d_lon = math.radians(b.longitude - a.longitude)
    h = math.sin(d_lat / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(d_lon / 2) ** 2
    return EARTH_RADIUS_METERS * 2 * math.atan2(math.sqrt(h), math.sqrt(1 - h))
