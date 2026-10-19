namespace AutoPowerMeter.Models;

/// <summary>
/// 单条用例的测量结果。MeasuredMa 统一 mA 存储。
/// </summary>
public sealed class RunResult
{
    public string TestCaseId { get; set; } = "";
    public double MeasuredMa { get; set; } = double.NaN;
    public ResultStatus Status { get; set; } = ResultStatus.Pending;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public DateTime? MeasurementStartedAt { get; set; }
    public DateTime? MeasurementFinishedAt { get; set; }
    /// <summary>仅首次判定 FAIL 时记录首测；正式报告的 MeasuredMa/采样时间以唯一一次复测为准。</summary>
    public bool RetestAttempted { get; set; }
    public double? InitialMeasuredMa { get; set; }
    public DateTime? InitialMeasurementStartedAt { get; set; }
    public DateTime? InitialMeasurementFinishedAt { get; set; }
    public double? InitialMeasurementDurationSeconds { get; set; }
    public DateTime? ActionsFinishedAt { get; set; }
    public string MeasurementVerification { get; set; } = "";
    public DateTimeOffset? DeviceStateCheckedAt { get; set; }
    public double? RequestedMeasurementDurationSeconds { get; set; }
    public double? ActualMeasurementDurationSeconds { get; set; }
    public StabilityEvidence? Stability { get; set; }
    public CaptureQuality? CaptureQuality { get; set; }
    public string? SourceResource { get; set; }
    public DateTime? WaveformStartedAt { get; set; }
    public DateTime? WaveformFinishedAt { get; set; }
    public int WaveformMeasurementStartIndex { get; set; }
    public int WaveformMeasurementSampleCount { get; set; }
    public double WaveformBeforeSeconds { get; set; }
    public double WaveformAfterSeconds { get; set; }
    public string WaveformNote { get; set; } = "";
    /// <summary>
    /// 本用例每个自动化动作的起止时间；与 ActionsFinishedAt、MeasurementStartedAt
    /// 共同构成可追溯的“动作完成 → 管线排空/稳定 → 正式采样”时间轴。
    /// </summary>
    public List<ActionTiming> ActionTimings { get; set; } = new();
    public string Note { get; set; } = "";

    /// <summary>原始样本（mA），可用于事后分析与画图，避免存太多——超过 10000 自动重采样。</summary>
    public List<double>? Samples { get; set; }
    public string? RawDataPath { get; set; }
    public string? AverageWaveformPath { get; set; }
}
