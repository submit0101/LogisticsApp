namespace LogisticsApp.Models;

public class GeoPoint
{
    public double Lat { get; set; }
    public double Lng { get; set; }

    public GeoPoint(double lat, double lng)
    {
        Lat = lat;
        Lng = lng;
    }
}