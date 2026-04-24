using System.Collections.Generic;

namespace LogisticsApp.Models;

public class HelpSection
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class HelpDocument
{
    public string ModuleTitle { get; set; } = string.Empty;
    public string IconKind { get; set; } = string.Empty;
    public List<HelpSection> Sections { get; set; } = new();
}