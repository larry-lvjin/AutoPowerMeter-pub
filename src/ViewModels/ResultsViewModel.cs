using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using AutoPowerMeter.Models;
using AutoPowerMeter.Utils;

namespace AutoPowerMeter.ViewModels;

public sealed class ResultsViewModel : ObservableObject
{
    public ObservableCollection<ResultRowViewModel> Rows { get; } = new();

    public RelayCommand ExportCsvCommand { get; }

    public ResultsViewModel()
    {
        ExportCsvCommand = new RelayCommand(ExportCsv, () => Rows.Count > 0);
    }

    public void AddOrReplace(TestCase tc, RunResult r)
    {
        var existing = Rows.FirstOrDefault(x => x.Id == tc.Id);
        if (existing is null)
            Rows.Add(new ResultRowViewModel(tc, r));
        else
            existing.Update(r);
        ExportCsvCommand.RaiseCanExecuteChanged();
    }

    private void ExportCsv()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"AutoPowerMeter-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder();
        sb.AppendLine("Id,Excel行,类别,项目,限值,实测,单位,状态,完成时间,备注,动作时间轴,最后动作结束时间,数据产生时间,实际采样时长秒,原始波形文件");
        foreach (var r in Rows)
        {
            var inUa = Math.Abs(r.MeasuredMa) < 1.0;
            var disp = inUa ? UnitFormatter.MaToUa(r.MeasuredMa) : r.MeasuredMa;
            var unit = inUa ? "uA" : "mA";
            sb.AppendLine(string.Join(",",
                Csv(r.Id), r.ExcelRow, Csv(r.Category), Csv(r.ProjectName), Csv(r.LimitText),
                double.IsNaN(disp) ? "" : disp.ToString("0.###", CultureInfo.InvariantCulture),
                unit, r.StatusLabel, r.FinishedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"), Csv(r.Note),
                Csv(r.ActionTimeline),
                r.ActionsFinishedAt?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "",
                r.MeasurementStartedAt?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "",
                r.ActualMeasurementDurationSeconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
                Csv(r.RawDataPath ?? "")));
        }
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        MessageBox.Show($"已导出: {dlg.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
