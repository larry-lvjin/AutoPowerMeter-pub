using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace AutoPowerMeter.Excel;

/// <summary>
/// 从功耗测试 xlsx 抽取每行的原始字段。不做语义解析（语义解析在 TestPlanGenerator）。
/// </summary>
public sealed class WorkbookReader
{
    private readonly ILogger<WorkbookReader> _logger;

    public WorkbookReader(ILogger<WorkbookReader> logger) { _logger = logger; }

    /// <summary>一行原始数据。列序按 xlsx 表头：A 类别, B 项目, C 简述, D 详步, ... G 参考标准, H... 设备列</summary>
    public sealed record RawRow(
        int RowNumber,
        string? Category,
        string? Project,
        string? DescriptionShort,
        string? DescriptionLong,
        string? Reference,
        string? DeviceCellOriginal);

    public sealed record WorkbookData(
        string FilePath,
        string SheetName,
        int DeviceColumnIndex,    // 1-based
        string DeviceColumnHeader,
        IReadOnlyList<RawRow> Rows);

    /// <summary>读取功耗 sheet，自动探测表头行（历史模板在第 1 行，新报告在第 10 行）。</summary>
    public WorkbookData Read(string filePath, string deviceSerial)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Contains("功耗", StringComparison.Ordinal))
                 ?? wb.Worksheet(1);

        var headerRowNumber = FindHeaderRow(ws);
        var headerRow = ws.Row(headerRowNumber);

        int deviceColIndex = -1;
        string deviceColHeader = "";
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 10;
        for (var c = 1; c <= lastCol; c++)
        {
            var v = headerRow.Cell(c).GetString();
            if (v.Contains(deviceSerial, StringComparison.OrdinalIgnoreCase))
            {
                deviceColIndex = c;
                deviceColHeader = v;
                break;
            }
        }
        if (deviceColIndex < 0)
        {
            // 兜底：G 之后第一个看起来像 SN/设备结果的列，否则 H。
            deviceColIndex = Enumerable.Range(8, Math.Max(1, lastCol - 7))
                .FirstOrDefault(c =>
                {
                    var value = headerRow.Cell(c).GetString();
                    return value.Contains("Iphone", StringComparison.OrdinalIgnoreCase)
                           || System.Text.RegularExpressions.Regex.IsMatch(value, @"\d{12,}");
                });
            if (deviceColIndex <= 0) deviceColIndex = 8;
            deviceColHeader = headerRow.Cell(deviceColIndex).GetString();
            _logger.LogWarning("device column for {sn} not found, fallback to column H ({hdr})", deviceSerial, deviceColHeader);
        }

        var rows = new List<RawRow>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = headerRowNumber + 1; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var raw = new RawRow(
                RowNumber: r,
                Category: NullIfEmpty(row.Cell(1).GetString()),
                Project: NullIfEmpty(row.Cell(2).GetString()),
                DescriptionShort: NullIfEmpty(ReadMergedCell(row.Cell(3))),
                DescriptionLong: NullIfEmpty(ReadMergedCell(row.Cell(4))),
                Reference: NullIfEmpty(row.Cell(7).GetString()),
                DeviceCellOriginal: NullIfEmpty(row.Cell(deviceColIndex).GetString()));
            rows.Add(raw);
        }

        return new WorkbookData(filePath, ws.Name, deviceColIndex, deviceColHeader, rows);
    }

    private static int FindHeaderRow(IXLWorksheet ws)
    {
        var max = Math.Min(30, ws.LastRowUsed()?.RowNumber() ?? 1);
        for (var r = 1; r <= max; r++)
        {
            var values = Enumerable.Range(1, Math.Min(12, ws.LastColumnUsed()?.ColumnNumber() ?? 12))
                .Select(c => ws.Cell(r, c).GetString()).ToArray();
            if (values.Any(v => v.Contains("测试类别", StringComparison.Ordinal))
                && values.Any(v => v.Contains("参考标准", StringComparison.Ordinal)))
                return r;
        }
        return 1;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string ReadMergedCell(IXLCell cell)
    {
        if (!cell.IsMerged()) return cell.GetString();
        var first = cell.MergedRange().FirstCell();
        // 纵向合并继承场景；横跨 C:D 的内容只读一次，避免重复步骤。
        return first.Address.ColumnNumber == cell.Address.ColumnNumber ? first.GetString() : "";
    }
}
