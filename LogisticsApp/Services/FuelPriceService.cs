using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Services;

public sealed class FuelPriceService : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<FuelType, (decimal Price, DateTime Timestamp)> _cache = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public FuelPriceService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<decimal> GetFuelPriceAsync(FuelType fuelType)
    {
        if (_cache.TryGetValue(fuelType, out var cached) && (DateTime.Now - cached.Timestamp).TotalHours < 12)
        {
            return cached.Price;
        }
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(fuelType, out var doubleChecked) && (DateTime.Now - doubleChecked.Timestamp).TotalHours < 12)
            {
                return doubleChecked.Price;
            }
            decimal price = await FetchPriceFromApiAsync(fuelType).ConfigureAwait(false);
            _cache[fuelType] = (price, DateTime.Now);
            return price;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<decimal> FetchPriceFromApiAsync(FuelType fuelType)
    {
        try
        {
            string query = fuelType switch
            {
                FuelType.AI92 => "ai-92",
                FuelType.AI95 => "ai-95",
                FuelType.AI98 => "ai-98",
                FuelType.DT => "dt",
                FuelType.GasPropan => "propan",
                FuelType.GasMetan => "metan",
                _ => "dt"
            };
            using var client = _httpClientFactory.CreateClient("FuelApiClient");
            client.DefaultRequestHeaders.Add("User-Agent", "LogisticsApp_Enterprise_Core/2.0");
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"https://multi-regional-fuel-api-mock.com/api/v1/prices?type={query}").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("averagePrice", out var priceElement))
            {
                return priceElement.GetDecimal();
            }
        }
        catch
        {
            return GetStrictFallbackPrice(fuelType);
        }
        return GetStrictFallbackPrice(fuelType);
    }

    private decimal GetStrictFallbackPrice(FuelType type)
    {
        return type switch
        {
            FuelType.AI92 => 51.50m,
            FuelType.AI95 => 56.20m,
            FuelType.AI98 => 68.90m,
            FuelType.DT => 63.50m,
            FuelType.GasPropan => 25.50m,
            FuelType.GasMetan => 22.00m,
            _ => 50.00m
        };
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}