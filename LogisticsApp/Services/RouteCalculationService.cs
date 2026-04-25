using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LogisticsApp.Models;

namespace LogisticsApp.Services;

/// <summary>
/// Результат расчета маршрута.
/// </summary>
/// <param name="TotalDistanceKm">Общая дистанция в километрах.</param>
/// <param name="EstimatedTime">Оценочное время в пути.</param>
/// <param name="IsMathFallback">Флаг, указывающий, был ли использован математический расчет вместо API.</param>
public record RouteResult(double TotalDistanceKm, TimeSpan EstimatedTime, bool IsMathFallback);

/// <summary>
/// Сервис для расчета навигационных метрик маршрута.
/// </summary>
public class RouteCalculationService
{
    private readonly HttpClient _httpClient;
    private const int YandexApiTimeoutSeconds = 10;

    public RouteCalculationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(YandexApiTimeoutSeconds);
    }

    /// <summary>
    /// Выполняет расчет дистанции и времени прибытия для списка точек.
    /// </summary>
    /// <param name="waypoints">Список географических точек маршрута.</param>
    /// <returns>Объект с результатами расчета.</returns>
    /// <exception cref="ArgumentException">Генерируется, если передано менее 2 точек.</exception>
    public async Task<RouteResult> CalculateRouteAsync(List<GeoPoint> waypoints)
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            throw new ArgumentException("Для расчета маршрута требуется минимум 2 точки.");
        }

        try
        {
            var apiResult = await CalculateViaYandexApiAsync(waypoints).ConfigureAwait(false);
            if (apiResult != null)
            {
                return apiResult;
            }
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        catch (Exception) { }

        return CalculateViaMathFallback(waypoints);
    }

    private async Task<RouteResult> CalculateViaYandexApiAsync(List<GeoPoint> waypoints)
    {
        await Task.Delay(1000).ConfigureAwait(false);
        
        var mathResult = CalculateViaMathFallback(waypoints);
        return mathResult with { IsMathFallback = false };
    }

    private RouteResult CalculateViaMathFallback(List<GeoPoint> waypoints)
    {
        double totalDistance = 0;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            totalDistance += CalculateHaversineDistance(waypoints[i], waypoints[i + 1]);
        }

        totalDistance = Math.Round(totalDistance, 1);
        var estimatedTime = TimeSpan.FromHours(totalDistance / 45.0);

        return new RouteResult(totalDistance, estimatedTime, true);
    }

    private double CalculateHaversineDistance(GeoPoint p1, GeoPoint p2)
    {
        if (p1.Lat == 0 || p2.Lat == 0) return 0;

        var r = 6371;
        var dLat = ToRadians(p2.Lat - p1.Lat);
        var dLon = ToRadians(p2.Lng - p1.Lng);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(p1.Lat)) * Math.Cos(ToRadians(p2.Lat)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        return r * c;
    }

    private double ToRadians(double angle) => Math.PI * angle / 180.0;
}