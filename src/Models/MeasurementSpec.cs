namespace AutoPowerMeter.Models;

/// <summary>
/// 测量需求：类型 + 持续时间 + 采样建议。
/// </summary>
public sealed class MeasurementSpec
{
    public MeasurementType Type { get; set; } = MeasurementType.Average;

    /// <summary>测量窗口秒数。Instant 时忽略。</summary>
    public int DurationSeconds { get; set; } = 60;

    /// <summary>采样频率 Hz。100K12++ 按 v6 协议使用 10 kSa/s。</summary>
    public double SampleRateHz { get; set; } = 10_000.0;

    /// <summary>量程提示。Auto 时驱动根据当前限值自动选择。</summary>
    public CurrentRange RangeHint { get; set; } = CurrentRange.Auto;

    /// <summary>旧版稳态门控配置；当前自动测试固定等待60秒，不启用此检查。</summary>
    public StabilitySpec Stability { get; set; } = new();
}

public sealed class StabilitySpec
{
    public bool Enabled { get; set; }
    public int WindowSeconds { get; set; } = 15;
    public int MaximumWaitSeconds { get; set; } = 60;
    public double RelativeTolerance { get; set; } = 0.08;
    public double AbsoluteToleranceMa { get; set; } = 0.010;
}
