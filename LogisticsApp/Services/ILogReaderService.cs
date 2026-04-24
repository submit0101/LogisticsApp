using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Models;

namespace LogisticsApp.Services;

public interface ILogReaderService
{
    Task<List<string>> GetAvailableLogFilesAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<LogEntry> ReadAndFilterLogsAsync(string filePath, string searchTerm, string levelFilter, CancellationToken cancellationToken = default);
}