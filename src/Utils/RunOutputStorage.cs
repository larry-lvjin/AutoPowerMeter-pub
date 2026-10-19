using System.IO;
using System.Text;
using System.Text.Json;
using AutoPowerMeter.Excel;
using AutoPowerMeter.TestRunner;

namespace AutoPowerMeter.Utils;

/// <summary>日志、报告和FAIL波形共用一个输出根目录；切换只复制当前数据，不删除历史。</summary>
public sealed class RunOutputStorage
{
    private readonly object _gate = new();
    private readonly StringBuilder _log = new();
    private readonly string _settingsPath;
    private readonly string _logName = $"AutoPowerMeter-运行日志-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.txt";
    private string _directory;
    private bool _logInitialized;
    public string DirectoryPath { get { lock (_gate) return _directory; } }
    public string LogPath { get { lock (_gate) return Path.Combine(_directory, _logName); } }
    public string? LastWriteError { get; private set; }

    public RunOutputStorage(string settingsPath)
    {
        _settingsPath = settingsPath;
        string? saved = null;
        try { saved = JsonSerializer.Deserialize<string>(File.ReadAllText(settingsPath)); } catch { }
        var requested = Environment.GetEnvironmentVariable(RunArtifactWriter.WaveformDirectoryEnvironmentVariable);
        _directory = Normalize(!string.IsNullOrWhiteSpace(requested) ? requested
            : !string.IsNullOrWhiteSpace(saved) ? saved : RunArtifactWriter.PreferredWaveformDirectory);
        Environment.SetEnvironmentVariable(RunArtifactWriter.WaveformDirectoryEnvironmentVariable, _directory);
        _log.AppendLine("AutoPowerMeter 运行日志").AppendLine($"启动时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}")
            .AppendLine(new string('-', 88));
        try { WriteSnapshot(_directory); _logInitialized = true; }
        catch (Exception ex) { LastWriteError = ex.Message; }
    }

    public void Append(LogEntry entry)
    {
        lock (_gate)
        {
            var line = $"[{entry.Time:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] [{entry.Category}] {entry.Message}";
            if (entry.Exception is not null) line += Environment.NewLine + entry.Exception;
            line += Environment.NewLine;
            _log.Append(line);
            try
            {
                if (!_logInitialized) WriteSnapshot(_directory);
                else File.AppendAllText(LogPath, line, new UTF8Encoding(false));
                _logInitialized = true; LastWriteError = null;
            }
            catch (Exception ex) { _logInitialized = false; LastWriteError = ex.Message; }
        }
    }

    public void ChangeDirectory(string directory, WorkbookWriter? report = null)
    {
        var next = Normalize(directory);
        lock (_gate)
        {
            // 先验证新目录能保存完整日志，再迁移工作簿。任何失败都不提交输出根目录。
            WriteSnapshot(next);
            report?.RelocateToDirectory(next);
            _directory = next;
            _logInitialized = true; LastWriteError = null;
            Environment.SetEnvironmentVariable(RunArtifactWriter.WaveformDirectoryEnvironmentVariable, next);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_settingsPath))!);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(next), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Append(new LogEntry(DateTime.Now, Microsoft.Extensions.Logging.LogLevel.Warning,
                    "输出目录", "当前目录已切换，但无法保存下次启动设置：" + ex.Message, null));
            }
        }
    }

    public string CreateReportPath(string suiteName)
    {
        var name = string.Concat(suiteName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (name.Length > 80) name = name[..80];
        return Path.Combine(DirectoryPath, $"{name}-功耗测试报告-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.xlsx");
    }

    private void WriteSnapshot(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, _logName), _log.ToString(), new UTF8Encoding(true));
    }

    private static string Normalize(string directory)
    {
        var path = directory.Trim();
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("保存目录必须是完整绝对路径，例如 D:\\AutoPowerWave");
        return Path.GetFullPath(path);
    }
}
