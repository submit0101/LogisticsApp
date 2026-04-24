using System.Threading;
using System.Threading.Tasks;

namespace LogisticsApp.Services;

public interface IDatabaseManagementService
{
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task BackupDatabaseAsync(string targetFilePath, CancellationToken cancellationToken = default);
    Task WipeDatabaseAsync();
    Task GenerateTestDataAsync(int count, string targetDictionary = "All");
}