using System;

namespace LogisticsApp.Models;

public sealed class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ExceptionDetails { get; set; } = string.Empty;
    public bool HasException => !string.IsNullOrWhiteSpace(ExceptionDetails);
}