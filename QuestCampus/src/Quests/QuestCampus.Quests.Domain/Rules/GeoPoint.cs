using QuestCampus.Quests.Domain.Common;

namespace QuestCampus.Quests.Domain.Rules;

/// <summary>Position GPS (WGS 84) en degrés décimaux. Objet valeur immuable.</summary>
public readonly record struct GeoPoint
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoPoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
            throw new DomainException("La latitude doit être comprise entre -90 et 90.");
        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
            throw new DomainException("La longitude doit être comprise entre -180 et 180.");

        Latitude = latitude;
        Longitude = longitude;
    }
}
