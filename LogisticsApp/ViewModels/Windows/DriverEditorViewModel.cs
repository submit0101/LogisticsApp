using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace LogisticsApp.ViewModels.Windows;

public partial class DriverEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbContextFactory;
    private readonly NotificationService _notify;
    private Driver _originalDriver = new();
    private bool _isNew;

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Фамилия обязательна")]
    private string _lastName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Имя обязательно")]
    private string _firstName = string.Empty;

    [ObservableProperty] private string? _middleName;
    [ObservableProperty] private string? _photoPath;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Номер ВУ обязателен")]
    private string _licenseNumber = string.Empty;

    // --- ЖЕЛЕЗОБЕТОННЫЕ СВОЙСТВА ДЛЯ КАТЕГОРИЙ (БЕЗ ГЕНЕРАТОРА) ---
    private bool _categoryB;
    public bool CategoryB { get => _categoryB; set => SetProperty(ref _categoryB, value); }

    private bool _categoryC;
    public bool CategoryC { get => _categoryC; set => SetProperty(ref _categoryC, value); }

    private bool _categoryC1;
    public bool CategoryC1 { get => _categoryC1; set => SetProperty(ref _categoryC1, value); }

    private bool _categoryCE;
    public bool CategoryCE { get => _categoryCE; set => SetProperty(ref _categoryCE, value); }

    private bool _categoryC1E;
    public bool CategoryC1E { get => _categoryC1E; set => SetProperty(ref _categoryC1E, value); }

    private bool _categoryBE;
    public bool CategoryBE { get => _categoryBE; set => SetProperty(ref _categoryBE, value); }

    private bool _categoryD;
    public bool CategoryD { get => _categoryD; set => SetProperty(ref _categoryD, value); }
    // -------------------------------------------------------------

    [ObservableProperty] private DateTime _licenseExpirationDate = DateTime.Today.AddYears(10);
    [ObservableProperty] private string? _passportData;
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Телефон обязателен")]
    private string _phone = string.Empty;
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [EmailAddress(ErrorMessage = "Неверный формат Email")]
    private string? _email;
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(DriverEditorViewModel), nameof(ValidateEmploymentDate))]
    private DateTime _employmentDate = DateTime.Today;
    [ObservableProperty] private DateTime? _dismissalDate;
    [ObservableProperty] private DriverStatus _status = DriverStatus.Active;
    [ObservableProperty] private string? _medicalCertificateNumber;
    [ObservableProperty] private DateTime? _medicalCertificateExpiration;
    [ObservableProperty] private string? _emergencyContact;
    [ObservableProperty] private string? _notes;

    public Array AvailableStatuses => Enum.GetValues(typeof(DriverStatus));

    public DriverEditorViewModel(IDbContextFactory<LogisticsDbContext> dbContextFactory, NotificationService notify)
    {
        _dbContextFactory = dbContextFactory;
        _notify = notify;
    }

    public void Initialize(Driver? driver)
    {
        _isNew = driver == null;
        _originalDriver = driver ?? new Driver();
        if (!_isNew) InitializeProperties(_originalDriver);
        else { EmploymentDate = DateTime.Today; LicenseExpirationDate = DateTime.Today.AddYears(10); }
        ValidateAllProperties();
    }

    private void InitializeProperties(Driver d)
    {
        LastName = d.LastName; FirstName = d.FirstName; MiddleName = d.MiddleName;
        PhotoPath = d.PhotoPath; LicenseNumber = d.LicenseNumber;
        LicenseExpirationDate = d.LicenseExpirationDate; PassportData = d.PassportData;
        Phone = d.Phone; Email = d.Email; EmploymentDate = d.EmploymentDate;
        DismissalDate = d.DismissalDate; Status = d.Status;
        MedicalCertificateNumber = d.MedicalCertificateNumber;
        MedicalCertificateExpiration = d.MedicalCertificateExpiration;
        EmergencyContact = d.EmergencyContact; Notes = d.Notes;

        if (!string.IsNullOrWhiteSpace(d.LicenseCategories))
        {
            var cats = d.LicenseCategories.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            CategoryB = cats.Contains("B");
            CategoryC = cats.Contains("C");
            CategoryC1 = cats.Contains("C1");
            CategoryCE = cats.Contains("CE");
            CategoryC1E = cats.Contains("C1E");
            CategoryBE = cats.Contains("BE");
            CategoryD = cats.Contains("D");
        }
    }

    public static ValidationResult? ValidateEmploymentDate(DateTime date, ValidationContext context) =>
        date.Date > DateTime.Today ? new ValidationResult("Дата найма не может быть из будущего") : ValidationResult.Success;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();

        if (!CategoryB && !CategoryC && !CategoryC1 && !CategoryCE && !CategoryC1E && !CategoryBE && !CategoryD)
        {
            _notify.Warning("Выберите хотя бы одну категорию прав.");
            return;
        }

        if (HasErrors) { _notify.Warning("Проверьте правильность заполнения полей."); return; }

        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            if (_isNew) { var n = new Driver(); UpdateModel(n); context.Drivers.Add(n); }
            else
            {
                var u = await context.Drivers.FindAsync(_originalDriver.DriverID);
                if (u != null) { context.Entry(u).OriginalValues["RowVersion"] = _originalDriver.RowVersion; UpdateModel(u); }
            }
            await context.SaveChangesAsync();
            _notify.Success("Данные сохранены.");
            RequestClose?.Invoke(true);
        }
        catch (Exception ex) { _notify.Error($"Ошибка: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private void UpdateModel(Driver model)
    {
        model.LastName = LastName; model.FirstName = FirstName; model.MiddleName = MiddleName;
        model.PhotoPath = PhotoPath; model.LicenseNumber = LicenseNumber;
        model.LicenseExpirationDate = LicenseExpirationDate; model.PassportData = PassportData;
        model.Phone = Phone; model.Email = Email; model.EmploymentDate = EmploymentDate;
        model.Status = Status; model.Notes = Notes;
        model.MedicalCertificateNumber = MedicalCertificateNumber;
        model.MedicalCertificateExpiration = MedicalCertificateExpiration;
        model.EmergencyContact = EmergencyContact;

        var selected = new List<string>();
        if (CategoryB) selected.Add("B");
        if (CategoryC) selected.Add("C");
        if (CategoryC1) selected.Add("C1");
        if (CategoryCE) selected.Add("CE");
        if (CategoryC1E) selected.Add("C1E");
        if (CategoryBE) selected.Add("BE");
        if (CategoryD) selected.Add("D");
        model.LicenseCategories = string.Join(", ", selected);
    }

    [RelayCommand] private void UploadPhoto() { /* Твоя логика загрузки фото */ }
    [RelayCommand] private void RemovePhoto() => PhotoPath = null;
    [RelayCommand] private void Cancel() => RequestClose?.Invoke(false);
}