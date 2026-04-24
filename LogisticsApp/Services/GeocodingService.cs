using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using LogisticsApp.Models;

namespace LogisticsApp.Services;

public class GeocodingService
{
    private readonly HttpClient _httpClient;
    private const string ApiKey = "d3d669be25f078cb7de5544574d6d9f759b58ddf";

    public GeocodingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        ConfigureDaDataClient(_httpClient);
    }

    public GeocodingService()
    {
        _httpClient = new HttpClient();
        ConfigureDaDataClient(_httpClient);
    }

    private void ConfigureDaDataClient(HttpClient client)
    {
        if (client.BaseAddress == null)
        {
            client.BaseAddress = new Uri("https://suggestions.dadata.ru/suggestions/api/4_1/rs/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", ApiKey);
        }
    }

    public async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        try
        {
            var request = new { query = address, count = 1 };
            var response = await _httpClient.PostAsJsonAsync("suggest/address", request);
            if (!response.IsSuccessStatusCode) return null;
            var jsonString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;
            if (root.TryGetProperty("suggestions", out var suggestions) && suggestions.GetArrayLength() > 0)
            {
                var data = suggestions[0].GetProperty("data");
                if (data.TryGetProperty("geo_lat", out var latElement) && latElement.ValueKind != JsonValueKind.Null &&
                    data.TryGetProperty("geo_lon", out var lonElement) && lonElement.ValueKind != JsonValueKind.Null)
                {
                    string? latString = latElement.GetString();
                    string? lonString = lonElement.GetString();
                    if (double.TryParse(latString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(lonString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                    {
                        return (lat, lon);
                    }
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetAddressFromCoordinatesAsync(double lat, double lon)
    {
        try
        {
            var request = new { lat = lat, lon = lon, count = 1, radius_meters = 50 };
            var response = await _httpClient.PostAsJsonAsync("geolocate/address", request);
            if (!response.IsSuccessStatusCode) return null;
            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;
            if (root.TryGetProperty("suggestions", out var suggestions) && suggestions.GetArrayLength() > 0)
            {
                return suggestions[0].GetProperty("value").GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public GeoPoint GetDepotLocation()
    {
        return new GeoPoint(53.208101, 34.444738);
    }
}