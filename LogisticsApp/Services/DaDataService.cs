using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LogisticsApp.Services;

public class DaDataPartyRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;
}

public class DaDataPartyResponse
{
    [JsonPropertyName("suggestions")]
    public DaDataSuggestion[] Suggestions { get; set; } = Array.Empty<DaDataSuggestion>();
}

public class DaDataSuggestion
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public DaDataPartyData? Data { get; set; }
}

public class DaDataPartyData
{
    [JsonPropertyName("kpp")]
    public string? Kpp { get; set; }

    [JsonPropertyName("ogrn")]
    public string? Ogrn { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public DaDataName? Name { get; set; }

    [JsonPropertyName("address")]
    public DaDataAddress? Address { get; set; }

    [JsonPropertyName("management")]
    public DaDataManagement? Management { get; set; }
}

public class DaDataName
{
    [JsonPropertyName("full_with_opf")]
    public string? FullWithOpf { get; set; }

    [JsonPropertyName("short_with_opf")]
    public string? ShortWithOpf { get; set; }
}

public class DaDataAddress
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class DaDataManagement
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class DaDataService
{
    private readonly HttpClient _httpClient;

    public DaDataService(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://suggestions.dadata.ru/suggestions/api/4_1/rs/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", settingsService.Current.DaDataApiKey);
    }

    public async Task<DaDataSuggestion?> GetPartyByInnAsync(string inn)
    {
        if (string.IsNullOrWhiteSpace(inn)) return null;
        var request = new DaDataPartyRequest { Query = inn };
        var response = await _httpClient.PostAsJsonAsync("findById/party", request);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<DaDataPartyResponse>();
        return result?.Suggestions?.FirstOrDefault();
    }
}