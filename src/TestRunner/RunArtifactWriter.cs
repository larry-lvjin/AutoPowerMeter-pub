using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AutoPowerMeter.Models;

namespace AutoPowerMeter.TestRunner;

public static class RunArtifactWriter
{
    public const int AverageWindowMilliseconds = 100;
    public const string WaveformDirectoryEnvironmentVariable = "AUTO_POWER_WAVEFORM_DIR";
    public static readonly string PreferredWaveformDirectory = @"D:\AutoPowerWave";

    public sealed record ArtifactPaths(string RawWaveformPath, string AverageWaveformPath, string MetadataPath,
        string MpaWaveformPath);

    public static bool ShouldWrite(ResultStatus status) => status == ResultStatus.Fail;

    public static ArtifactPaths Write(TestCase testCase, RunResult result, double sampleRateHz,
        IReadOnlyList<double> rawSamples)
    {
        var runDirectory = ResolveRunDirectory(result);
        Directory.CreateDirectory(runDirectory);
        var safeId = string.Concat(testCase.Id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var stamp = (result.MeasurementStartedAt ?? result.StartedAt).ToString("yyyyMMdd-HHmmss-fff");
        var fileStem = $"{stamp}-{safeId}";
        var csvPath = Path.Combine(runDirectory, fileStem + "-100ms-csv.csv");
        var periodMs = 1000.0 / Math.Max(0.1, sampleRateHz);
        var capturedAt = result.WaveformStartedAt ?? result.MeasurementStartedAt ?? result.StartedAt;
        var serial = (result.SourceResource ?? "").Split("::").FirstOrDefault(p => p.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) ?? "COM0";
        serial = string.Concat(serial.Where(char.IsAsciiLetterOrDigit));
        // 与 EMK850x 原生文件名保持一致；用例ID记录在同目录 markers/json 中。
        var mpaPath = Path.Combine(runDirectory, $"{capturedAt:yyyy-MM-dd-HH-mm-ss}-1ms-{serial}.mpa");
        // Match Docs/波形/波形模板.csv: UTF-8 BOM, source name, Chinese header,
        // then 100 ms windows with elapsed time, wall-clock time, min/mean/max mA.
        // The companion MPA file retains 1 ms averages and extrema.
        var averagePath = csvPath;
        var samplesPerWindow = Math.Max(1, (int)Math.Round(sampleRateHz * AverageWindowMilliseconds / 1000.0));
        using (var writer = new StreamWriter(csvPath, false, new UTF8Encoding(true)))
        {
            writer.WriteLine(fileStem + "-100ms-csv.mpa");
            writer.WriteLine("时间：100ms,绝对时间：,最小电流mA：,平均电流mA：,最大电流mA：");
            for (var start = 0; start < rawSamples.Count; start += samplesPerWindow)
            {
                var end = Math.Min(rawSamples.Count, start + samplesPerWindow);
                double sum = 0, min = double.MaxValue, max = double.MinValue;
                for (var i = start; i < end; i++)
                {
                    var value = rawSamples[i];
                    sum += value;
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                }
                var elapsed = TimeSpan.FromSeconds(start / sampleRateHz);
                var hours = (long)elapsed.TotalHours;
                var microseconds = (elapsed.Ticks % TimeSpan.TicksPerMillisecond) / 10;
                writer.Write(hours.ToString(CultureInfo.InvariantCulture));
                writer.Write($":{elapsed.Minutes:00}:{elapsed.Seconds:00}:{elapsed.Milliseconds:000}:{microseconds:000},");
                writer.Write(capturedAt.Add(elapsed).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(min.ToString("0.######", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write((sum / (end - start)).ToString("0.######", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.WriteLine(max.ToString("0.######", CultureInfo.InvariantCulture));
            }
        }

        MpaWaveformWriter.Write(mpaPath, rawSamples, sampleRateHz);
        var markersPath = Path.Combine(runDirectory, fileStem + ".markers.txt");
        File.WriteAllText(markersPath,
            $"用例：{testCase.Id} {testCase.ProjectName}{Environment.NewLine}" +
            $"完整操作：{testCase.Description.Replace("\r", " ").Replace("\n", " | ")}{Environment.NewLine}" +
            $"波形：{Path.GetFileName(mpaPath)}{Environment.NewLine}" +
            $"波形起点（主机时间推算）：{capturedAt:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}" +
            $"用例开始：{result.StartedAt:yyyy-MM-dd HH:mm:ss.fff}（波形 {OffsetSeconds(result.StartedAt, capturedAt):F3} 秒）{Environment.NewLine}" +
            string.Concat(result.ActionTimings.Select(action =>
                $"动作：{action.Action} | {action.Description} | " +
                $"{action.StartedAt:HH:mm:ss.fff}～{action.FinishedAt:HH:mm:ss.fff} | " +
                $"波形 {OffsetSeconds(action.StartedAt, capturedAt):F3}～{OffsetSeconds(action.FinishedAt, capturedAt):F3} 秒 | " +
                $"{(action.Succeeded ? "成功" : "失败")}{Environment.NewLine}")) +
            (result.Stability is null ? "" :
                $"稳态门控：{result.Stability.MonitoringStartedAt:HH:mm:ss.fff}～{result.Stability.ConfirmedAt:HH:mm:ss.fff} | " +
                $"波形 {OffsetSeconds(result.Stability.MonitoringStartedAt, capturedAt):F3}～{OffsetSeconds(result.Stability.ConfirmedAt, capturedAt):F3} 秒{Environment.NewLine}") +
            (result.DeviceStateCheckedAt is null ? "" :
                $"采样前页面树/设备真值验收：{result.DeviceStateCheckedAt:HH:mm:ss.fff} | " +
                $"波形 {OffsetSeconds(result.DeviceStateCheckedAt.Value.LocalDateTime, capturedAt):F3} 秒 | " +
                $"{result.MeasurementVerification}{Environment.NewLine}") +
            (result.MeasurementStartedAt is null ? "" :
                $"正式采样主机时间：{result.MeasurementStartedAt:HH:mm:ss.fff}～{result.MeasurementFinishedAt:HH:mm:ss.fff}{Environment.NewLine}") +
            (result.RetestAttempted
                ? $"首测 FAIL：{result.InitialMeasuredMa:F6} mA | " +
                  $"{result.InitialMeasurementStartedAt:HH:mm:ss.fff}～{result.InitialMeasurementFinishedAt:HH:mm:ss.fff}；" +
                  $"复测 60 秒：{result.MeasuredMa:F6} mA，最终以复测为准{Environment.NewLine}"
                : "") +
            $"正式取值：波形第 {result.WaveformMeasurementStartIndex / sampleRateHz:F3} 秒至 " +
            $"{(result.WaveformMeasurementStartIndex + result.WaveformMeasurementSampleCount) / sampleRateHz:F3} 秒（右端不包含）{Environment.NewLine}" +
            $"用例开始至正式取值：{result.WaveformBeforeSeconds:F3} 秒；正式取值后至判定完成：{result.WaveformAfterSeconds:F3} 秒{Environment.NewLine}" +
            $"判定值：{result.MeasuredMa:F6} mA，{result.Status}，只使用正式取值窗口，未剔除脉冲{Environment.NewLine}" +
            $"CSV为100ms最小/平均/最大电流；MPA为1ms平均及极值记录，未保存逐点CSV。{Environment.NewLine}" +
            result.WaveformNote, new UTF8Encoding(false));
        var metadataPath = Path.Combine(runDirectory, fileStem + ".json");
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(new
        {
            testCase.Id,
            testCase.ExcelRow,
            testCase.Category,
            testCase.ProjectName,
            testCase.Description,
            testCase.LimitText,
            result.MeasuredMa,
            status = result.Status.ToString(),
            result.StartedAt,
            result.FinishedAt,
            result.MeasurementStartedAt,
            result.MeasurementFinishedAt,
            result.RetestAttempted,
            result.InitialMeasuredMa,
            result.InitialMeasurementStartedAt,
            result.InitialMeasurementFinishedAt,
            result.InitialMeasurementDurationSeconds,
            result.ActionsFinishedAt,
            result.MeasurementVerification,
            result.DeviceStateCheckedAt,
            testCase.ReuseRecordingFromCaseId,
            testCase.ReuseSceneFromCaseId,
            result.RequestedMeasurementDurationSeconds,
            result.ActualMeasurementDurationSeconds,
            result.Stability,
            result.CaptureQuality,
            result.WaveformStartedAt,
            result.WaveformFinishedAt,
            result.WaveformBeforeSeconds,
            result.WaveformAfterSeconds,
            result.WaveformMeasurementStartIndex,
            result.WaveformMeasurementSampleCount,
            result.WaveformNote,
            measurementStartOffsetSeconds = result.WaveformMeasurementStartIndex / sampleRateHz,
            measurementEndOffsetSeconds = (result.WaveformMeasurementStartIndex + result.WaveformMeasurementSampleCount) / sampleRateHz,
            result.ActionTimings,
            sampleRateHz,
            samplePeriodMilliseconds = periodMs,
            averageWindowMilliseconds = AverageWindowMilliseconds,
            sampleCount = rawSamples.Count,
            timestampSource = "estimated waveform start + sample index / configured sample rate; not hardware timestamps",
            mpaWaveform = Path.GetFileName(mpaPath),
            waveformMarkers = Path.GetFileName(markersPath),
            mpaRecordPeriodMilliseconds = 1,
            mpaLastRecordRawSampleCount = rawSamples.Count % Math.Max(1, (int)(sampleRateHz / 1000)) is var remainder && remainder != 0
                ? remainder : Math.Max(1, (int)(sampleRateHz / 1000)),
            rawWaveform = Path.GetFileName(csvPath),
            averageWaveform = Path.GetFileName(averagePath),
            waveformFormat = "100ms min/mean/max mA; UTF-8 BOM; Docs/波形/波形模板.csv"
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return new ArtifactPaths(csvPath, averagePath, metadataPath, mpaPath);
    }

    private static string ResolveRunDirectory(RunResult result)
    {
        var configured = Environment.GetEnvironmentVariable(WaveformDirectoryEnvironmentVariable);
        var root = !string.IsNullOrWhiteSpace(configured) ? configured : PreferredWaveformDirectory;
        return Path.Combine(root, result.StartedAt.ToString("yyyyMMdd-HHmmss"));
    }

    private static double OffsetSeconds(DateTime time, DateTime origin) => (time - origin).TotalSeconds;
}
