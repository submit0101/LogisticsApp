using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace LogisticsApp.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbContextFactory;
    private readonly string _credentialsFilePath;

    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public AuthService(IDbContextFactory<LogisticsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogisticsApp");
        Directory.CreateDirectory(appDataPath);
        _credentialsFilePath = Path.Combine(appDataPath, "credentials.dat");
    }

    public async Task<User?> AuthenticateAsync(string login, string password)
    {
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            // ИСПРАВЛЕНИЕ: Возвращена глубокая загрузка матрицы прав (RBAC)
            var user = await context.Users
                .Include(u => u.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Login == login);

            if (user == null)
            {
                Log.Warning("Неудачная попытка входа: пользователь {Login} не найден.", login);
                return null;
            }

            if (VerifyPassword(password, user.PasswordHash))
            {
                Log.Information("Пользователь {Login} успешно авторизован.", login);
                return user;
            }

            Log.Warning("Неудачная попытка входа: неверный пароль для {Login}.", login);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обращении к БД во время авторизации пользователя {Login}.", login);
            throw;
        }
    }

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithm,
            KeySize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithm,
            KeySize);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public void SaveCredentials(string username, string password)
    {
        try
        {
            var data = $"{username}\0{password}";
            var rawBytes = Encoding.UTF8.GetBytes(data);
            var encryptedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_credentialsFilePath, encryptedBytes);
            Log.Information("Учетные данные для {Login} успешно зашифрованы и сохранены.", username);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении учетных данных через DPAPI.");
        }
    }

    public bool LoadCredentials(out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        if (!File.Exists(_credentialsFilePath)) return false;

        try
        {
            var encryptedBytes = File.ReadAllBytes(_credentialsFilePath);
            var rawBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var data = Encoding.UTF8.GetString(rawBytes);
            var parts = data.Split('\0');

            if (parts.Length == 2)
            {
                username = parts[0];
                password = parts[1];
                return true;
            }
        }
        catch (CryptographicException)
        {
            Log.Warning("Не удалось расшифровать файл учетных данных DPAPI. Файл будет удален.");
            ClearCredentials();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при загрузке сохраненных учетных данных.");
        }

        return false;
    }

    public void ClearCredentials()
    {
        if (File.Exists(_credentialsFilePath))
        {
            File.Delete(_credentialsFilePath);
            Log.Information("Файл сохраненных учетных данных удален.");
        }
    }
}