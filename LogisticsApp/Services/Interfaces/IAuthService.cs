using System.Threading.Tasks;
using LogisticsApp.Models;

namespace LogisticsApp.Services.Interfaces;

public interface IAuthService
{
    Task<User?> AuthenticateAsync(string login, string password);
    string HashPassword(string password);
    void SaveCredentials(string username, string password);
    bool LoadCredentials(out string username, out string password);
    void ClearCredentials();
}