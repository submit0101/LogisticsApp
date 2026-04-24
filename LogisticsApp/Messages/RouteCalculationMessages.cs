using System;
using System.Collections.Generic;
using LogisticsApp.Models;

namespace LogisticsApp.Messages;

public class RouteCalculationRequestMessage
{
    public Guid RequestId { get; }
    public List<GeoPoint> Points { get; }
    public bool Optimize { get; }

    public RouteCalculationRequestMessage(Guid requestId, List<GeoPoint> points, bool optimize)
    {
        RequestId = requestId;
        Points = points;
        Optimize = optimize;
    }
}

public class RouteCalculationResponseMessage
{
    public Guid RequestId { get; }
    public double DistanceKm { get; }
    public double TimeMinutes { get; }
    public double TimeInTrafficMinutes { get; }
    public List<int>? OptimizedOrder { get; }

    public RouteCalculationResponseMessage(Guid requestId, double distanceKm, double timeMinutes, double timeInTrafficMinutes, List<int>? optimizedOrder)
    {
        RequestId = requestId;
        DistanceKm = distanceKm;
        TimeMinutes = timeMinutes;
        TimeInTrafficMinutes = timeInTrafficMinutes;
        OptimizedOrder = optimizedOrder;
    }
}

public class RouteInteractiveUpdateMessage
{
    public double DistanceKm { get; }
    public double TimeMinutes { get; }
    public double TimeInTrafficMinutes { get; }

    public RouteInteractiveUpdateMessage(double distanceKm, double timeMinutes, double timeInTrafficMinutes)
    {
        DistanceKm = distanceKm;
        TimeMinutes = timeMinutes;
        TimeInTrafficMinutes = timeInTrafficMinutes;
    }
}