using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
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

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Фамилия обязательна")]
    [MinLength(2, ErrorMessage = "Минимум 2 символа")]
    private string _lastName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Имя обязательно")]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string? _middleName;

    [ObservableProperty]
    private string? _photoPath;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Номер ВУ обязателен")]
    private string _licenseNumber = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Категории обязательны")]
    private string _licenseCategories = string.Empty;

    [ObservableProperty]
    private DateTime _licenseExpirationDate = DateTime.Today.AddYears(10);

    [ObservableProperty]
    private string? _passportData;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Телефон обязателен")]
    private string _phone = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [EmailAddress(ErrorMessage = "Неверный формат Email")]
    private string? _email;

    // ВАЛИДАЦИЯ ДАТЫ НАЙМА (Свойство + CustomValidation)
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(DriverEditorViewModel), nameof(ValidateEmploymentDate))]
    private DateTime _employmentDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _dismissalDate;

    [ObservableProperty]
    private DriverStatus _status = DriverStatus.Active;

    [ObservableProperty]
    private string? _medicalCertificateNumber;

    [ObservableProperty]
    private DateTime? _medicalCertificateExpiration;

    [ObservableProperty]
    private string? _emergencyContact;

    [ObservableProperty]
    private string? _notes;

    public Array AvailableStatuses => Enum.GetValues(typeof(DriverStatus));

    public string[] AvailableCategories { get; } = { "B", "C", "D", "E", "CE", "C, CE" };

    public DriverEditorViewModel(IDbContextFactory<LogisticsDbContext> dbContextFactory, NotificationService notify)
    {
        _dbContextFactory = dbContextFactory;
        _notify = notify;
    }

    public void Initialize(Driver? driver)
    {
        _isNew = driver == null;
        _originalDriver = driver ?? new Driver();

        if (!_isNew)
        {
            InitializeProperties(_originalDriver);
        }
        else
        {
            EmploymentDate = DateTime.Today;
            LicenseExpirationDate = DateTime.Today.AddYears(10);
        }

        ValidateAllProperties();
    }

    private void InitializeProperties(Driver d)
    {
        LastName = d.LastName;
        FirstName = d.FirstName;
        MiddleName = d.MiddleName;
        PhotoPath = d.PhotoPath;
        LicenseNumber = d.LicenseNumber;
        LicenseCategories = d.LicenseCategories;
        LicenseExpirationDate = d.LicenseExpirationDate;
        PassportData = d.PassportData;
        Phone = d.Phone;
        Email = d.Email;
        EmploymentDate = d.EmploymentDate;
        DismissalDate = d.DismissalDate;
        Status = d.Status;
        MedicalCertificateNumber = d.MedicalCertificateNumber;
        MedicalCertificateExpiration = d.MedicalCertificateExpiration;
        EmergencyContact = d.EmergencyContact;
        Notes = d.Notes;
    }

    // МЕТОД ПЕРЕХВАТА: Дата найма не может быть из будущего
    public static ValidationResult? ValidateEmploymentDate(DateTime date, ValidationContext context)
    {
        if (date.Date > DateTime.Today)
            return new ValidationResult("Дата найма не может быть позже сегодняшнего дня");
        return ValidationResult.Success;
    }

    [RelayCommand]
    private void UploadPhoto()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Изображения|*.jpg;*.jpeg;*.png",
            Title = "Выберите фото водителя"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Avatars");
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            var extension = Path.GetExtension(openFileDialog.FileName);
            var newFileName = $"{Guid.NewGuid()}{extension}";
            var targetPath = Path.Combine(targetFolder, newFileName);

            File.Copy(openFileDialog.FileName, targetPath, true);
            PhotoPath = targetPath;
        }
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        PhotoPath = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();

        if (HasErrors)
        {
            _notify.Warning("Пожалуйста, заполните все обязательные поля корректно.");
            return;
        }

        // МЕТОД ПЕРЕХВАТА: Проверка истечения срока действия ВУ
        if (LicenseExpirationDate.Date < DateTime.Today)
        {
            var msgResult = MessageBox.Show(
                "Срок действия водительского удостоверения истек!\n\nВы уверены, что хотите продолжить сохранение карточки?",
                "Внимание: ВУ просрочено",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (msgResult != MessageBoxResult.Yes) return;
        }

        // МЕТОД ПЕРЕХВАТА: Проверка истечения срока действия Медицинской справки
        if (MedicalCertificateExpiration.HasValue && MedicalCertificateExpiration.Value.Date < DateTime.Today)
        {
            var msgResult = MessageBox.Show(
                "Срок действия медицинской справки истек!\n\nВы уверены, что хотите продолжить сохранение карточки?",
                "Внимание: Медсправка просрочена",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (msgResult != MessageBoxResult.Yes) return;
        }

        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            if (_isNew)
            {
                var newDriver = new Driver();
                UpdateModel(newDriver);
                context.Drivers.Add(newDriver);
            }
            else
            {
                var driverToUpdate = await context.Drivers.FindAsync(_originalDriver.DriverID);
                if (driverToUpdate == null) return;

                context.Entry(driverToUpdate).OriginalValues["RowVersion"] = _originalDriver.RowVersion;
                UpdateModel(driverToUpdate);
            }

            await context.SaveChangesAsync();
            _notify.Success("Карточка водителя успешно сохранена");
            RequestClose?.Invoke(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            _notify.Error("Данные были изменены другим пользователем. Сохранение отменено.");
            RequestClose?.Invoke(false);
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка сохранения: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void UpdateModel(Driver model)
    {
        model.LastName = LastName;
        model.FirstName = FirstName;
        model.MiddleName = MiddleName;
        model.PhotoPath = PhotoPath;
        model.LicenseNumber = LicenseNumber;
        model.LicenseCategories = LicenseCategories;
        model.LicenseExpirationDate = LicenseExpirationDate;
        model.PassportData = PassportData;
        model.Phone = Phone;
        model.Email = Email;
        model.EmploymentDate = EmploymentDate;
        model.DismissalDate = DismissalDate;
        model.Status = Status;
        model.MedicalCertificateNumber = MedicalCertificateNumber;
        model.MedicalCertificateExpiration = MedicalCertificateExpiration;
        model.EmergencyContact = EmergencyContact;
        model.Notes = Notes;
    }
}