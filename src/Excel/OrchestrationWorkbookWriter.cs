using AutoPowerMeter.Models;
using ClosedXML.Excel;
using System.IO;

namespace AutoPowerMeter.Excel;

/// <summary>导出模板全部项目的场景化执行清单；取消项保留用于覆盖审计。</summary>
public static class OrchestrationWorkbookWriter
{
    public static void Write(string outputPath, IReadOnlyList<TestCase> cases)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("全量场景编排");
        var headers = new[]
        {
            "执行顺序", "模板行", "场景ID", "测试类别", "测试项目", "初始场景", "场景内操作",
            "等待与采样", "原始测试方法", "参考标准", "自动化等级", "执行通道", "RecPlay脚本", "风险/解析备注", "承接用例", "源表行", "实际动作顺序", "共用结果行"
        };
        for (var column = 1; column <= headers.Length; column++) sheet.Cell(1, column).Value = headers[column - 1];

        for (var index = 0; index < cases.Count; index++)
        {
            var test = cases[index];
            var row = index + 2;
            sheet.Cell(row, 1).Value = test.ExecutionOrder;
            sheet.Cell(row, 2).Value = test.ExcelRow;
            sheet.Cell(row, 3).Value = test.ScenarioId;
            sheet.Cell(row, 4).Value = test.Category;
            sheet.Cell(row, 5).Value = $"【{test.MeasurementKind}】{test.ProjectName}";
            sheet.Cell(row, 6).Value = test.InitialScene;
            sheet.Cell(row, 7).Value = test.SceneOperation;
            sheet.Cell(row, 8).Value = test.MeasurementAction;
            sheet.Cell(row, 9).Value = test.Description;
            sheet.Cell(row, 10).Value = test.LimitText;
            sheet.Cell(row, 11).Value = test.AutomationLevel.ToString();
            sheet.Cell(row, 12).Value = test.AutomationChannel;
            sheet.Cell(row, 13).Value = test.RecordingScript;
            sheet.Cell(row, 14).Value = string.Join("；", test.ParseNotes);
            sheet.Cell(row, 15).Value = test.AuthoritativeSequence ? test.PreviousSequenceCaseId : test.ReuseRecordingFromCaseId;
            sheet.Cell(row, 16).Value = test.SequenceSourceRow;
            sheet.Cell(row, 17).Value = string.Join(" → ", test.Steps.Select(s => s.ToString()));
            sheet.Cell(row, 18).Value = string.Join("、", test.SharedResultRows.Select(r => $"T{r:D3}"));
        }

        var range = sheet.Range(1, 1, cases.Count + 1, headers.Length);
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.WrapText = true;
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns(1, 2).Width = 10;
        sheet.Column(3).Width = 38;
        sheet.Columns(4, 5).Width = 24;
        sheet.Columns(6, 8).Width = 48;
        sheet.Column(9).Width = 72;
        sheet.Columns(10, 15).Width = 22;

        var summary = workbook.AddWorksheet("覆盖统计");
        summary.Cell("A1").Value = "模板总项目"; summary.Cell("B1").Value = cases.Count;
        summary.Cell("A2").Value = "自动执行"; summary.Cell("B2").Value = cases.Count(x => x.AutomationLevel == AutomationLevel.Auto);
        summary.Cell("A3").Value = "已取消（需人工）"; summary.Cell("B3").Value = cases.Count(x => x.AutomationLevel is AutomationLevel.Manual or AutomationLevel.Cancelled);
        summary.Cell("A5").Value = "固定规则";
        summary.Cell("B5").Value = "电压≤4.0V；电源重启后预录音3秒；自动项操作完成后等待60秒，再以10 kSa/s连续采样60秒并取算术平均；FAIL保存波形。";
        summary.Cell("A6").Value = "取消规则";
        summary.Cell("B6").Value = "录音笔亮屏、传输回归、纯白屏、船运、OTA待机、充电回归、斜放姿态、关机均手动；其他能力限制详见每项备注。";
        var pairs = cases.Where(x => !string.IsNullOrEmpty(x.ReuseRecordingFromCaseId)).ToArray();
        summary.Cell("A7").Value = "录音→待机配对"; summary.Cell("B7").Value = pairs.Length;
        summary.Cell("A8").Value = "省去重复录音次数（满足复用时）";
        summary.Cell("B8").Value = pairs.Count(x => x.Steps.Any(s => s.Action == AutomationActionKind.StartRecording));
        summary.Cell("A9").Value = "省去录音保持秒数（满足复用时）";
        summary.Cell("B9").Value = pairs.SelectMany(x => x.Steps).Where(s => s.Action == AutomationActionKind.Wait).Sum(s => s.DurationSeconds);
        summary.Cell("A10").Value = "复用边界";
        summary.Cell("B10").Value = "仅相邻成功录音且SN/模式/连接/FindMy/电压相符时续接；失败或单独运行走独立准备；绑定阶段先测完再解绑，不重新绑定。";
        if (cases.Any(t => t.AuthoritativeSequence))
        {
            summary.Cell("A3").Value = "需手动测试";
            summary.Cell("B3").Value = cases.Count(t => t.AutomationLevel == AutomationLevel.Cancelled && t.ResultReuseFromCaseId.Length == 0);
            summary.Cell("A4").Value = "共用结果（不重复执行）";
            summary.Cell("B4").Value = cases.Count(t => t.ResultReuseFromCaseId.Length > 0);
            summary.Cell("B6").Value = "仅执行用户原表中的91项；其他82项手动，17项引用结果。";
            summary.Cell("A7").Value = "源表过场数"; summary.Cell("B7").Value = cases.Sum(t => t.TransitionDescriptions.Count);
            summary.Range("A8:B9").Clear(XLClearOptions.Contents);
            summary.Cell("B10").Value = "严格按Delta自动化实现.xlsx行序；增量继承场景及录音/暂停；前项失败不得以不完整动作重建；仅按过场19显式绑定Find My。";
            summary.Cell("A11").Value = "编排唯一来源";
            summary.Cell("B11").Value = cases.First(t => t.AuthoritativeSequence).SequenceSource;
        }
        summary.Columns().AdjustToContents(10, 80);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        workbook.SaveAs(outputPath);
    }
}
