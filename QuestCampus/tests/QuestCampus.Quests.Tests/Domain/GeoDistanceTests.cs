using QuestCampus.Quests.Domain.Common;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Tests.Domain;

public class GeoDistanceTests
{
    [Fact]
    public void Distance_between_identical_points_is_zero()
    {
        var point = new GeoPoint(48.85646, 2.35242);
        Assert.Equal(0, GeoDistance.HaversineMeters(point, point), precision: 6);
    }

    [Fact]
    public void One_degree_of_latitude_is_about_111_km()
    {
        var distance = GeoDistance.HaversineMeters(new GeoPoint(0, 0), new GeoPoint(1, 0));
        Assert.InRange(distance, 111_190, 111_200);
    }

    [Fact]
    public void Paris_to_London_is_about_344_km()
    {
        var notreDame = new GeoPoint(48.8530, 2.3499);
        var bigBen = new GeoPoint(51.5007, -0.1246);
        Assert.InRange(GeoDistance.HaversineMeters(notreDame, bigBen) / 1000, 338, 345);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        var a = new GeoPoint(48.85646, 2.35242);
        var b = new GeoPoint(48.85797, 2.34888);
        Assert.Equal(GeoDistance.HaversineMeters(a, b), GeoDistance.HaversineMeters(b, a), precision: 6);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    public void Invalid_coordinates_are_rejected(double latitude, double longitude)
    {
        Assert.Throws<DomainException>(() => new GeoPoint(latitude, longitude));
    }
}
