using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LogisticsApp.ViewModels.Windows;

public partial class CustomerEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly GeocodingService _geoService;
    private readonly DaDataService _daDataService;
    private readonly OverlayService _overlay;
    private Customer _currentCustomer = new();
    private CancellationTokenSource? _debounceCts;

    // Флаг для предотвращения "прыжков" маркера и зацикливания геокодера
    private bool _isUpdatingFromMap;

    public event Action<bool>? RequestClose;
    public event Action<double, double, string>? OnMapLocationChanged;

    [ObservableProperty]
    private ObservableCollection<string> _availableBanks = new();

    [ObservableProperty]
    private int _innMaxLength = 10;

    [ObservableProperty]
    private int _ogrnMaxLength = 13;

    [ObservableProperty]
    private string _ogrnLabel = "ОГРН:";

    [ObservableProperty]
    private int _phoneMaskType = 0;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Выберите вид контрагента")]
    private CustomerType _type = CustomerType.LegalEntity;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(CustomerEditorViewModel), nameof(ValidateInn))]
    private string _inn = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(CustomerEditorViewModel), nameof(ValidateKpp))]
    private string _kpp = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(CustomerEditorViewModel), nameof(ValidateOgrn))]
    private string _ogrn = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Краткое наименование обязательно для заполнения")]
    [MinLength(3, ErrorMessage = "Наименование должно содержать минимум 3 символа")]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _legalAddress = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Укажите фактический адрес (для логистики)")]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _contactPerson = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(30, ErrorMessage = "Слишком длинный номер телефона")]
    private string _phone = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^(|.+@.+\..+)$", ErrorMessage = "Укажите корректный E-mail (например, name@domain.com)")]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^(|\d{9})$", ErrorMessage = "БИК должен состоять строго из 9 цифр")]
    private string _bik = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(200, ErrorMessage = "Слишком длинное наименование банка")]
    private string _bankName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^(|\d{20})$", ErrorMessage = "Расчетный счет должен состоять строго из 20 цифр")]
    private string _checkingAccount = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^(|\d{20})$", ErrorMessage = "Корреспондентский счет должен состоять строго из 20 цифр")]
    private string _corrAccount = string.Empty;

    [ObservableProperty] private double? _geoLat;
    [ObservableProperty] private double? _geoLon;

    public List<CustomerType> AvailableTypes { get; } = new()
    {
        CustomerType.LegalEntity,
        CustomerType.Entrepreneur,
        CustomerType.PhysicalPerson
    };

    public CustomerEditorViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        DaDataService daDataService,
        GeocodingService geoService,
        OverlayService overlay)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _daDataService = daDataService;
        _geoService = geoService;
        _overlay = overlay;

        AvailableBanks = new ObservableCollection<string>
        {
            "Сбербанк", "ВТБ", "Газпромбанк", "Альфа-Банк", "Россельхозбанк",
            "Московский Кредитный Банк", "Банк «Открытие»", "Совкомбанк",
            "Райффайзенбанк", "Росбанк", "Т-Банк (Тинькофф)", "ЮниКредит Банк",
            "Банк ДОМ.РФ", "Промсвязьбанк", "Банк «Санкт-Петербург»",
            "Уралсиб", "Ак Барс Банк", "Новикомбанк", "МТС Банк",
            "Точка", "Модульбанк", "Авангард", "Ренессанс Кредит", "ОТП Банк",
            "Кредит Европа Банк", "Почта Банк", "Русский Стандарт", "СМП Банк",
            "УБРиР", "РНКБ", "Бланк Банк", "Делобанк", "Центр-инвест"
        };
    }

    public void Initialize(Customer? customer)
    {
        if (customer != null)
        {
            _currentCustomer = customer;
            Type = customer.Type;
            UpdateDynamicLengths();

            Inn = customer.INN ?? "";
            Kpp = customer.KPP ?? "";
            Ogrn = customer.OGRN ?? "";
            Name = customer.Name;
            FullName = customer.FullName ?? "";
            LegalAddress = customer.LegalAddress ?? "";

            _isUpdatingFromMap = true;
            Address = customer.Address;
            _isUpdatingFromMap = false;

            ContactPerson = customer.ContactPerson ?? "";
            Phone = customer.Phone ?? "";
            Email = customer.Email ?? "";
            Bik = customer.BIK ?? "";
            BankName = customer.BankName ?? "";
            CheckingAccount = customer.CheckingAccount ?? "";
            CorrAccount = customer.CorrAccount ?? "";
            GeoLat = customer.GeoLat;
            GeoLon = customer.GeoLon;
        }
        else
        {
            _currentCustomer = new Customer();
            UpdateDynamicLengths();
        }

        ValidateAllProperties();
    }

    partial void OnTypeChanged(CustomerType value)
    {
        UpdateDynamicLengths();
        ValidateAllProperties();
    }

    private void UpdateDynamicLengths()
    {
        if (Type == CustomerType.LegalEntity)
        {
            InnMaxLength = 10;
            OgrnMaxLength = 13;
            OgrnLabel = "ОГРН (13 цифр):";
        }
        else if (Type == CustomerType.Entrepreneur)
        {
            InnMaxLength = 12;
            OgrnMaxLength = 15;
            OgrnLabel = "ОГРНИП (15 цифр):";
        }
        else
        {
            InnMaxLength = 12;
            OgrnMaxLength = 15;
            OgrnLabel = "ОГРН / ОГРНИП:";
        }
    }

    partial void OnAddressChanged(string value)
    {
        // Защита: Если адрес вставлен геокодером после клика по карте, не запускаем цикл заново!
        if (_isUpdatingFromMap) return;

        ProcessAddressGeocodingAsync(value).ConfigureAwait(false);
    }

    // ИНТЕЛЛЕКТУАЛЬНЫЙ ФОЛЛБЕК ГЕОКОДЕРА (Адаптирован под проблемы с запятыми)
    private async Task<(double Latitude, double Longitude)?> GetCoordinatesWithFallbackAsync(string targetAddress, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(targetAddress)) return null;

        // 1. Попытка: Ищем по полному точному адресу
        var coords = await _geoService.GetCoordinatesAsync(targetAddress);
        if (coords.HasValue || token.IsCancellationRequested) return coords;

        // 2. Попытка (Fallback): Разбиваем адрес на части. 
        // Если есть запятые, используем их, иначе пробелы.
        var parts = targetAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                                 .ToList();

        if (parts.Count <= 1)
        {
            parts = targetAddress.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                                 .ToList();
        }

        bool fallbackUsed = false;
        string fallbackAddress = targetAddress;

        while (parts.Count > 1)
        {
            if (token.IsCancellationRequested) return null;

            parts.RemoveAt(parts.Count - 1);

            // ВАЖНО: Склеиваем остаток через ПРОБЕЛ. 
            // Геокодер DaData лучше понимает нестрогие запросы (без запятых) через свой полнотекстовый поиск.
            fallbackAddress = string.Join(" ", parts);
            coords = await _geoService.GetCoordinatesAsync(fallbackAddress);

            if (coords.HasValue)
            {
                fallbackUsed = true;
                break;
            }
        }

        if (fallbackUsed && coords.HasValue && !token.IsCancellationRequested)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Info($"Точная гео-точка не найдена. Маркер установлен приблизительно: {fallbackAddress}");
            });
        }

        return coords;
    }

    private async Task ProcessAddressGeocodingAsync(string address)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(800, token);

            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(address))
                return;

            var coords = await GetCoordinatesWithFallbackAsync(address, token);

            if (coords.HasValue && !token.IsCancellationRequested)
            {
                GeoLat = coords.Value.Latitude;
                GeoLon = coords.Value.Longitude;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnMapLocationChanged?.Invoke(GeoLat.Value, GeoLon.Value, Name ?? "Контрагент");
                });
            }
        }
        catch (TaskCanceledException) { }
    }

    public static ValidationResult? ValidateInn(string inn, ValidationContext context)
    {
        var instance = (CustomerEditorViewModel)context.ObjectInstance;
        if (string.IsNullOrWhiteSpace(inn))
            return new ValidationResult("ИНН обязателен для заполнения");

        if (instance.Type == CustomerType.LegalEntity && inn.Length != 10)
            return new ValidationResult("Для Юридического лица ИНН должен содержать строго 10 цифр");

        if ((instance.Type == CustomerType.Entrepreneur || instance.Type == CustomerType.PhysicalPerson) && inn.Length != 12)
            return new ValidationResult("Для ИП и Физ. лица ИНН должен содержать строго 12 цифр");

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateKpp(string kpp, ValidationContext context)
    {
        var instance = (CustomerEditorViewModel)context.ObjectInstance;
        if (instance.Type == CustomerType.LegalEntity)
        {
            if (string.IsNullOrWhiteSpace(kpp) || kpp.Length != 9)
                return new ValidationResult("Для Юридического лица КПП обязателен (строго 9 цифр)");
        }
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateOgrn(string ogrn, ValidationContext context)
    {
        var instance = (CustomerEditorViewModel)context.ObjectInstance;
        if (!string.IsNullOrWhiteSpace(ogrn))
        {
            if (instance.Type == CustomerType.LegalEntity && ogrn.Length != 13)
                return new ValidationResult("ОГРН должен содержать строго 13 цифр");
            if (instance.Type == CustomerType.Entrepreneur && ogrn.Length != 15)
                return new ValidationResult("ОГРНИП должен содержать строго 15 цифр");
        }
        return ValidationResult.Success;
    }

    [RelayCommand]
    private async Task FillByInnAsync()
    {
        if (string.IsNullOrWhiteSpace(Inn)) return;

        bool isFound = false;

        string? newType = null, newShortName = null, newFullName = null, newKpp = null;
        string? newOgrn = null, newAddress = null, newManagement = null, suggestionValue = null;

        // Оверлей отвечает только за долгий HTTP запрос
        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            var suggestion = await _daDataService.GetPartyByInnAsync(Inn);
            if (suggestion != null && suggestion.Data != null)
            {
                isFound = true;
                newType = suggestion.Data.Type;
                newShortName = suggestion.Data.Name?.ShortWithOpf;
                newFullName = suggestion.Data.Name?.FullWithOpf;
                suggestionValue = suggestion.Value;
                newKpp = suggestion.Data.Kpp;
                newOgrn = suggestion.Data.Ogrn;
                newAddress = suggestion.Data.Address?.Value;
                newManagement = suggestion.Data.Management?.Name;
            }
        }, "Поиск в реестре ФНС...");

        // Продолжаем работу в безопасном UI-потоке после закрытия оверлея
        if (isFound)
        {
            Type = newType == "INDIVIDUAL" ? CustomerType.Entrepreneur : CustomerType.LegalEntity;
            Name = newShortName ?? suggestionValue ?? "";
            FullName = newFullName ?? suggestionValue ?? "";
            Kpp = newKpp ?? "";
            Ogrn = newOgrn ?? "";

            // Жестко перезаписываем оба адреса данными из реестра, блокируя двойной геокодинг
            _isUpdatingFromMap = true;
            LegalAddress = newAddress ?? "";
            Address = LegalAddress;
            _isUpdatingFromMap = false;

            ContactPerson = newManagement ?? "";

            ValidateAllProperties();
            _notify.Success("Реквизиты успешно заполнены по ИНН");

            // Форсируем отрисовку новой точки на карте.
            await GeocodeAndUpdateMapAsync(Address);
        }
        else
        {
            _notify.Warning("Организация с таким ИНН не найдена в реестре ФНС.");
        }
    }

    public async Task GeocodeAndUpdateMapAsync(string targetAddress)
    {
        _debounceCts?.Cancel(); // Глушим фоновый таймер от OnAddressChanged

        if (string.IsNullOrWhiteSpace(targetAddress)) return;

        // Вызываем новый интеллектуальный геокодер
        var coords = await GetCoordinatesWithFallbackAsync(targetAddress);

        if (coords.HasValue)
        {
            GeoLat = coords.Value.Latitude;
            GeoLon = coords.Value.Longitude;

            // Безопасный вызов события для карты
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnMapLocationChanged?.Invoke(coords.Value.Latitude, coords.Value.Longitude, Name ?? "Контрагент");
            });
        }
    }

    [RelayCommand]
    private async Task UpdateCoordinatesFromMapAsync(string jsonPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;

            if (root.TryGetProperty("lat", out var latProp) && root.TryGetProperty("lon", out var lonProp))
            {
                GeoLat = latProp.GetDouble();
                GeoLon = lonProp.GetDouble();

                OnMapLocationChanged?.Invoke(GeoLat.Value, GeoLon.Value, Name ?? "Контрагент");

                // Делаем обратное геокодирование
                var newAddress = await _geoService.GetAddressFromCoordinatesAsync(GeoLat.Value, GeoLon.Value);

                if (!string.IsNullOrEmpty(newAddress))
                {
                    // Блокируем триггер OnAddressChanged, чтобы геокодер не сдвинул маркер обратно на улицу!
                    _isUpdatingFromMap = true;
                    Address = newAddress;
                    _isUpdatingFromMap = false;

                    _notify.Success("Адрес и координаты успешно обновлены по выбранной точке на карте.");
                }
                else
                {
                    _notify.Info("Координаты обновлены, но точный адрес не найден.");
                }
            }
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка обновления координат: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            var errors = GetErrors().Select(e => e.ErrorMessage).Distinct().ToList();
            string errorMessage = "Пожалуйста, исправьте следующие ошибки:\n\n• " + string.Join("\n• ", errors);
            _notify.Warning(errorMessage);
            return;
        }

        _currentCustomer.Type = Type;
        _currentCustomer.INN = Inn;
        _currentCustomer.KPP = string.IsNullOrWhiteSpace(Kpp) ? null : Kpp;
        _currentCustomer.OGRN = string.IsNullOrWhiteSpace(Ogrn) ? null : Ogrn;
        _currentCustomer.Name = Name;
        _currentCustomer.FullName = string.IsNullOrWhiteSpace(FullName) ? null : FullName;
        _currentCustomer.LegalAddress = string.IsNullOrWhiteSpace(LegalAddress) ? null : LegalAddress;
        _currentCustomer.Address = Address;
        _currentCustomer.ContactPerson = string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson;
        _currentCustomer.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone;
        _currentCustomer.Email = string.IsNullOrWhiteSpace(Email) ? null : Email;
        _currentCustomer.BIK = string.IsNullOrWhiteSpace(Bik) ? null : Bik;
        _currentCustomer.BankName = string.IsNullOrWhiteSpace(BankName) ? null : BankName;
        _currentCustomer.CheckingAccount = string.IsNullOrWhiteSpace(CheckingAccount) ? null : CheckingAccount;
        _currentCustomer.CorrAccount = string.IsNullOrWhiteSpace(CorrAccount) ? null : CorrAccount;
        _currentCustomer.GeoLat = GeoLat;
        _currentCustomer.GeoLon = GeoLon;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (_currentCustomer.CustomerID == 0)
            {
                context.Customers.Add(_currentCustomer);
            }
            else
            {
                context.Customers.Update(_currentCustomer);
            }

            await context.SaveChangesAsync();
            RequestClose?.Invoke(true);
            _notify.Success("Контрагент успешно сохранен в базу данных");
        }
        catch (DbUpdateException dbEx)
        {
            string dbError = dbEx.InnerException?.Message ?? dbEx.Message;
            _notify.Error($"Ошибка базы данных:\n{dbError}");
        }
        catch (Exception ex)
        {
            _notify.Error($"Системная ошибка:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}