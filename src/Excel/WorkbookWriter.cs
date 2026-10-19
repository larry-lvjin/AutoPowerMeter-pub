using System.IO;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using AutoPowerMeter.Models;
using AutoPowerMeter.Utils;
using AutoPowerMeter.TestRunner;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPowerMeter.Excel;

/// <summary>
/// 把测试结果写回原 xlsx 设备列。每条用例测完立即保存（防崩溃丢数据）。
/// 跑批开始前自动备份原文件。
/// </summary>
public sealed class WorkbookWriter : IDisposable
{
    private readonly ILogger<WorkbookWriter> _logger;
    private string _filePath;
    private readonly HashSet<string> _ownedPaths = new(StringComparer.OrdinalIgnoreCase);
    public string FilePath { get { lock (_lock) return _filePath; } }
    private readonly int _deviceColIndex;
    private bool _hOnly;
    private readonly string _sheetName;
    private readonly object _lock = new();
    private XLWorkbook? _wb;
    private bool _disposed;

    private WorkbookWriter(ILogger<WorkbookWriter> logger, string filePath, string sheetName, int deviceColIndex)
    {
        _logger = logger;
        _filePath = filePath;
        _ownedPaths.Add(Path.GetFullPath(filePath));
        _sheetName = sheetName;
        _deviceColIndex = deviceColIndex;
    }

    public static WorkbookWriter OpenAndBackup(ILogger<WorkbookWriter> logger, Excel.WorkbookReader.WorkbookData data,
        string? resultFilePath = null, string? reportTemplatePath = null, string? sequencePath = null,
        string? standardsPath = null, int standardsColumnIndex = 6,
        int reportStandardsColumnIndex = 7, int reportResultColumnIndex = 8)
    {
        var targetPath = string.IsNullOrWhiteSpace(resultFilePath) ? data.FilePath : resultFilePath;
        var templatePath = !string.IsNullOrWhiteSpace(reportTemplatePath) && File.Exists(reportTemplatePath)
            ? reportTemplatePath
            : data.FilePath;

        if (File.Exists(targetPath))
        {
            var bakName = Path.Combine(
                Path.GetDirectoryName(targetPath)!,
                $"{Path.GetFileNameWithoutExtension(targetPath)}.bak.{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(targetPath)}");
            File.Copy(targetPath, bakName, overwrite: false);
            logger.LogInformation("backed up xlsx to {bak}", bakName);
        }

        var initializedFromTemplate = !Path.GetFullPath(templatePath)
            .Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase);
        if (initializedFromTemplate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(templatePath, targetPath, overwrite: true);
            logger.LogInformation("created report from template {template}: {path}", templatePath, targetPath);
        }

        var hOnly = AuthoritativeDeltaPlan.IsDelta(data.FilePath);
        var w = new WorkbookWriter(logger, targetPath, data.SheetName,
            hOnly ? reportResultColumnIndex : data.DeviceColumnIndex) { _hOnly = hOnly };
        w._wb = new XLWorkbook(targetPath);
        if (initializedFromTemplate)
        {
            var sheet = w._wb.Worksheet(data.SheetName);
            var plan = hOnly ? new TestPlanGenerator(NullLogger<TestPlanGenerator>.Instance)
                .Generate(data, sequencePath, standardsPath, standardsColumnIndex) : null;
            var automaticRows = plan?.Where(t => t.AutomationLevel == AutomationLevel.Auto || t.ResultReuseFromCaseId.Length > 0)
                .Select(t => t.ExcelRow).ToHashSet();
            var excludedRows = plan?.Where(t => t.ExcludedByUser).Select(t => t.ExcelRow).ToHashSet();
            if (hOnly && sequencePath is not null
                && Path.GetFileName(sequencePath).Contains("Deltas + Delta", StringComparison.OrdinalIgnoreCase))
            {
                var basic = Path.GetFileName(sequencePath).Contains("基本功耗", StringComparison.OrdinalIgnoreCase);
                using var standardsBook = new XLWorkbook(sequencePath);
                foreach (var sourceRow in standardsBook.Worksheet(1).RowsUsed())
                {
                    var idText = sourceRow.Cell(basic ? 1 : 2).GetString().Trim();
                    var match = System.Text.RegularExpressions.Regex.Match(idText, @"^T0*(\d+)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!match.Success) continue;
                    var reportRow = int.Parse(match.Groups[1].Value);
                    sheet.Cell(reportRow, 7).Value = sourceRow.Cell(basic ? 8 : 6).GetString().Trim();
                    sheet.Cell(reportRow, 8).Value = sourceRow.Cell(basic ? 9 : 7).GetString().Trim();
                }
            }
            if (hOnly && plan is not null)
                foreach (var test in plan.Where(t => t.AutomationLevel == AutomationLevel.Auto))
                    sheet.Cell(test.ExcelRow, reportStandardsColumnIndex).Value = test.LimitText;
            foreach (var row in data.Rows.Where(row =>
                         !string.IsNullOrWhiteSpace(row.Category)
                         || !string.IsNullOrWhiteSpace(row.Project)
                         || !string.IsNullOrWhiteSpace(row.DescriptionShort)
                         || !string.IsNullOrWhiteSpace(row.DescriptionLong)))
            {
                var cell = sheet.Cell(row.RowNumber, w._deviceColIndex);
                cell.Value = excludedRows?.Contains(row.RowNumber) == true ? "已剔除"
                    : automaticRows?.Contains(row.RowNumber) == true ? "" : "需手动测试";
                cell.Style.Fill.BackgroundColor = XLColor.NoColor;
                if (hOnly)
                {
                    var statusCell = sheet.Cell(row.RowNumber, w._deviceColIndex + 1);
                    statusCell.Value = excludedRows?.Contains(row.RowNumber) == true ? "已剔除"
                        : automaticRows?.Contains(row.RowNumber) == true ? "" : "待手动测试";
                    statusCell.Style.Fill.BackgroundColor = XLColor.NoColor;
                }
                if (!hOnly) sheet.Cell(row.RowNumber, data.DeviceColumnIndex + 1).Clear(XLClearOptions.Contents);
            }
            var firstDataRow = data.Rows.Count == 0 ? 2 : data.Rows.Min(row => row.RowNumber);
            if (!hOnly)
            {
                sheet.Cell(Math.Max(1, firstDataRow - 1), data.DeviceColumnIndex + 1).Value = "数据产生时间";
                sheet.Column(data.DeviceColumnIndex + 1).Width = 22;
            }
            w._wb.Save();
            logger.LogInformation("initialized Excel report; unmeasured rows marked 需手动测试: {path}",
                targetPath);
        }
        return w;
    }

    /// <summary>把当前报告复制到新目录，后续写入跟随；原文件保留。</summary>
    public void RelocateToDirectory(string directory)
    {
        lock (_lock)
        {
            if (_disposed || _wb is null) throw new ObjectDisposedException(nameof(WorkbookWriter));
            var target = Path.GetFullPath(Path.Combine(directory, Path.GetFileName(_filePath)));
            if (target.Equals(Path.GetFullPath(_filePath), StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(directory);
            if (File.Exists(target) && !_ownedPaths.Contains(target))
                target = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(target)}-{Guid.NewGuid():N}.xlsx");
            _wb.SaveAs(target); // 保留已有测量行及模板格式，后续Save写向新位置。
            _filePath = target;
            _ownedPaths.Add(target);
        }
    }

    public void SaveCopy(string path)
    {
        lock (_lock)
        {
            if (_disposed || _wb is null) throw new ObjectDisposedException(nameof(WorkbookWriter));
            _wb.Save();
            if (!Path.GetFullPath(path).Equals(Path.GetFullPath(_filePath), StringComparison.OrdinalIgnoreCase))
                File.Copy(_filePath, path, overwrite: true);
        }
    }

    /// <summary>把单条结果写入 xlsx，立刻保存。</summary>
    public void WriteResult(TestCase tc, RunResult result)
    {
        if (tc.ResultReuseFromCaseId.Length > 0) return; // 复用行仅由来源项回填，不能被手动/跳过状态覆盖。
        if (_wb is null) throw new InvalidOperationException("writer not opened");
        if (_disposed) throw new ObjectDisposedException(nameof(WorkbookWriter));

        lock (_lock)
        {
            var ws = _wb.Worksheet(_sheetName);
            foreach (var row in new[] { tc.ExcelRow }.Concat(tc.SharedResultRows).Distinct())
            {
                var cell = ws.Cell(row, _deviceColIndex);
                var timestampCell = _hOnly ? null : ws.Cell(row, _deviceColIndex + 1);
                var statusCell = _hOnly ? ws.Cell(row, _deviceColIndex + 1) : null;

                if (tc.ExcludedByUser)
                {
                    cell.Value = "已剔除";
                    timestampCell?.Clear(XLClearOptions.Contents);
                }
                else if (tc.AutomationLevel is AutomationLevel.Manual or AutomationLevel.Cancelled)
                {
                    cell.Value = "需手动测试";
                    timestampCell?.Clear(XLClearOptions.Contents);
                }
                else if (result.Status == ResultStatus.Skipped)
                {
                    cell.Value = "跳过";
                    timestampCell?.Clear(XLClearOptions.Contents);
                }
                else if (result.Status == ResultStatus.Error)
                {
                    cell.Value = $"错误: {result.Note}";
                    if (timestampCell is not null) timestampCell.Value = result.MeasurementStartedAt ?? result.FinishedAt;
                }
                else if (double.IsNaN(result.MeasuredMa))
                {
                    cell.Value = "—";
                    if (timestampCell is not null) timestampCell.Value = result.MeasurementStartedAt ?? result.FinishedAt;
                }
                else
                {
                    // 数值仍以 Excel 数字保存，通过格式显示单位，便于后续统计与筛选。
                    var inUa = tc.Limit.OriginalInMicroAmps;
                    var displayValue = UnitFormatter.NormalizeForExcel(result.MeasuredMa, inUa);
                    cell.Value = displayValue;
                    cell.Style.NumberFormat.Format = inUa ? "0\" uA\"" : "0.00\" mA\"";
                    if (timestampCell is not null) timestampCell.Value = result.MeasurementStartedAt ?? result.FinishedAt;
                }

                if (timestampCell is not null && !timestampCell.IsEmpty())
                    timestampCell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss.000";

                // 报告模板要求：正常值保持原表底色，异常数据统一黄色醒目标记。
                var isAbnormal = result.Status is ResultStatus.Fail or ResultStatus.Error;
                cell.Style.Fill.BackgroundColor = isAbnormal
                    ? XLColor.FromArgb(0xFF, 0xF2, 0xC0)
                    : XLColor.NoColor;
                if (statusCell is not null)
                {
                    statusCell.Value = tc.ExcludedByUser ? "已剔除"
                        : tc.AutomationLevel is AutomationLevel.Manual or AutomationLevel.Cancelled ? "待手动测试"
                        : result.Status switch
                        {
                            ResultStatus.Pass => "PASS",
                            ResultStatus.Fail => "FAIL",
                            ResultStatus.Warning => "WARNING",
                            ResultStatus.Error => "ERROR",
                            ResultStatus.Skipped => "SKIPPED",
                            _ => "未测试"
                        };
                    statusCell.Style.Fill.BackgroundColor = isAbnormal
                        ? XLColor.FromArgb(0xFF, 0xF2, 0xC0) : XLColor.NoColor;
                }
            }
            _wb.Save();
        }
        _logger.LogInformation("xlsx wrote row {r} col {c}: {v} [{s}]", tc.ExcelRow, _deviceColIndex, result.MeasuredMa, result.Status);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _wb?.Dispose(); } catch { /* ignore */ }
        _wb = null;
    }
}
