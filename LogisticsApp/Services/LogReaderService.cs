using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Models;

namespace LogisticsApp.Services;

public sealed class LogReaderService : ILogReaderService
{
    private readonly string _logDirectory;

    public LogReaderService()
    {
        _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogisticsApp", "Logs");
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public Task<List<string>> GetAvailableLogFilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(_logDirectory)) return [];
            return Directory.GetFiles(_logDirectory, "*.txt")
                            .Select(Path.GetFileName)
                            .Where(f => f != null)
                            .OrderByDescending(f => f)
                            .ToList()!;
        }, cancellationToken);
    }

    public async IAsyncEnumerable<LogEntry> ReadAndFilterLogsAsync(
        string filePath,
        string searchTerm,
        string levelFilter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_logDirectory, filePath);
        if (!File.Exists(fullPath)) yield break;

        bool hasSearch = !string.IsNullOrWhiteSpace(searchTerm);
        bool filterLevel = levelFilter != "Все";

        LogEntry? currentEntry = null;
        var messageBuilder = new StringBuilder();
        var exceptionBuilder = new StringBuilder();
        bool isParsingException = false;

        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(fileStream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (TryParseLogHeader(line, out var timestamp, out var level, out var initialMessage))
            {
                if (currentEntry != null)
                {
                    currentEntry.Message = messageBuilder.ToString().TrimEnd();
                    currentEntry.ExceptionDetails = exceptionBuilder.ToString().TrimEnd();

                    if (IsEntryValid(currentEntry, searchTerm, hasSearch, levelFilter, filterLevel))
                    {
                        yield return currentEntry;
                    }
                }

                currentEntry = new LogEntry
                {
                    Timestamp = timestamp,
                    Level = level
                };
                messageBuilder.Clear();
                messageBuilder.Append(initialMessage);
                exceptionBuilder.Clear();
                isParsingException = false;
            }
            else
            {
                if (currentEntry != null)
                {
                    if (line.Contains("Exception") || line.TrimStart().StartsWith("at "))
                    {
                        isParsingException = true;
                    }

                    if (isParsingException)
                    {
                        exceptionBuilder.AppendLine(line);
                    }
                    else
                    {
                        messageBuilder.AppendLine();
                        messageBuilder.Append(line);
                    }
                }
            }
        }

        if (currentEntry != null)
        {
            currentEntry.Message = messageBuilder.ToString().TrimEnd();
            currentEntry.ExceptionDetails = exceptionBuilder.ToString().TrimEnd();

            if (IsEntryValid(currentEntry, searchTerm, hasSearch, levelFilter, filterLevel))
            {
                yield return currentEntry;
            }
        }
    }

    private bool IsEntryValid(LogEntry entry, string search, bool hasSearch, string levelFilter, bool filterLevel)
    {
        if (filterLevel && !entry.Level.Contains(levelFilter, StringComparison.OrdinalIgnoreCase)) return false;

        if (hasSearch)
        {
            return entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   entry.ExceptionDetails.Contains(search, StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }

    private bool TryParseLogHeader(string line, out DateTime timestamp, out string level, out string message)
    {
        timestamp = default;
        level = string.Empty;
        message = string.Empty;

        if (line.Length < 25) return false;

        var span = line.AsSpan();

        int bracketIndex = span.IndexOf('[');
        if (bracketIndex == -1) return false;

        int closeBracketIndex = span.Slice(bracketIndex).IndexOf(']');
        if (closeBracketIndex == -1) return false;
        closeBracketIndex += bracketIndex;

        var dateSpan = span.Slice(0, bracketIndex).Trim();
        if (!DateTime.TryParse(dateSpan, out timestamp)) return false;

        level = span.Slice(bracketIndex + 1, closeBracketIndex - bracketIndex - 1).Trim().ToString();

        if (closeBracketIndex + 1 < span.Length)
        {
            message = span.Slice(closeBracketIndex + 1).TrimStart().ToString();
        }

        return true;
    }
}