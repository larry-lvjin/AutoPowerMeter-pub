using System.IO;

namespace AutoPowerMeter.Models;

public sealed record TestSuiteOption(
    string DisplayName,
    string FilePath,
    string ReportTemplatePath,
    string? SequencePath = null,
    string? StandardsPath = null,
    int StandardsColumnIndex = 6,
    int ReportStandardsColumnIndex = 7,
    int ReportResultColumnIndex = 8)
{
    public string FileName => Path.GetFileName(FilePath);
}
