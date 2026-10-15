namespace QuestCampus.Quests.Domain.Rules;

/// <summary>Calcul de distance à vol d'oiseau entre deux points GPS.</summary>
public static class GeoDistance
{
    /// <summary>Rayon moyen de la Terre en mètres (valeur IUGG).</summary>
    public const double EarthRadiusMeters = 6_371_008.8;

    /// <summary>
    /// Formule de haversine : distance sur la sphère, stable numériquement pour les petites distances
    /// (contrairement à la loi des cosinus), ce qui est exactement notre cas (rayon de 30 m).
    /// </summary>
    public static double HaversineMeters(GeoPoint from, GeoPoint to)
    {
        var lat1 = ToRadians(from.Latitude);
        var lat2 = ToRadians(to.Latitude);
        var deltaLat = ToRadians(to.Latitude - from.Latitude);
        var deltaLon = ToRadians(to.Longitude - from.Longitude);

        var a = Math.Pow(Math.Sin(deltaLat / 2), 2)
              + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
