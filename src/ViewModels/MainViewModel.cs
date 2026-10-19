using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace AutoPowerMeter.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly List<LogEntry> _logArchive = new();

    public ResultsViewModel Results { get; }
    public TestPlanViewModel TestPlan { get; }
    public ObservableCollection<LogEntry> Logs { get; } = new();
    public RelayCommand ExportLogCommand { get; }

    public MainViewModel()
    {
        Results = new ResultsViewModel();
        ExportLogCommand = new RelayCommand(ExportLog, () => _logArchive.Count > 0);
        ServiceLocator.Current.LogSink.OnLog += entry =>
        {
            void Add()
            {
                _logArchive.Add(entry);
                Logs.Add(entry);
                while (Logs.Count > 1000) Logs.RemoveAt(0);
                ExportLogCommand.RaiseCanExecuteChanged();
            }
            if (Application.Current.Dispatcher.CheckAccess()) Add();
            else Application.Current.Dispatcher.BeginInvoke(Add);
        };

        var connect = new ConnectViewModel();
        var phone = new PhoneViewModel();
        TestPlan = new TestPlanViewModel(connect, phone, Results);
    }

    private void ExportLog()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出运行日志",
            Filter = "文本文档 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            DefaultExt = ".txt",
            AddExtension = true,
            InitialDirectory = ServiceLocator.Current.OutputStorage.DirectoryPath,
            FileName = $"AutoPowerMeter-运行日志-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dialog.ShowDialog() != true) return;

        var builder = new StringBuilder();
        builder.AppendLine("AutoPowerMeter 运行日志");
        builder.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        builder.AppendLine($"记录数量：{_logArchive.Count}");
        builder.AppendLine(new string('-', 88));
        foreach (var entry in _logArchive)
        {
            builder.Append('[').Append(entry.Time.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ")
                .Append('[').Append(entry.Level).Append("] ")
                .Append('[').Append(entry.Category).Append("] ")
                .AppendLine(entry.Message);
            if (entry.Exception is not null)
                builder.AppendLine(entry.Exception.ToString());
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        MessageBox.Show($"运行日志已导出：\n{dialog.FileName}", "导出完成",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
