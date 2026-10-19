using AutoPowerMeter.Models;

namespace AutoPowerMeter.Instruments;

public static class PowerSafety
{
    public const double DefaultTestVoltageV = 4.0;
    public const double MaximumDeviceVoltageV = 4.0;

    public static void ValidateVoltage(double volts)
    {
        if (!double.IsFinite(volts) || volts is < 0 or > MaximumDeviceVoltageV)
            throw new ArgumentOutOfRangeException(nameof(volts), volts,
                $"样机保护：输出电压必须在 0–{MaximumDeviceVoltageV:0.0} V 范围内");
    }
}

/// <summary>
/// 程控电源抽象。MeasureCurrentMaAsync 返回单次电流读数 (mA)。
/// 所有方法线程安全，可在任意线程调用；实现内部需要时自行做锁/队列。
/// </summary>
public interface IPowerSource : IAsyncDisposable
{
    bool IsConnected { get; }
    string? ResourceName { get; }
    string? IdentificationString { get; }

    Task OpenAsync(string resourceName, CancellationToken ct = default);
    Task CloseAsync();

    Task ResetAsync(CancellationToken ct = default);

    Task SetVoltageAsync(double volts, CancellationToken ct = default);
    Task SetCurrentLimitAsync(double amps, CancellationToken ct = default);
    Task SetOutputAsync(bool on, CancellationToken ct = default);
    Task SetRangeAsync(CurrentRange range, CancellationToken ct = default);

    Task<double> GetVoltageAsync(CancellationToken ct = default);
    Task<double> MeasureVoltageMvAsync(CancellationToken ct = default);
    Task<double> MeasureCurrentMaAsync(CancellationToken ct = default);
}

/// <summary>
/// 支持从仪器上传流中无损抓取每个采样点的电源。
/// 普通轮询接口无法可靠达到 10 kSa/s，因此高速波形必须走此接口。
/// </summary>
public interface IStreamingPowerSource
{
    Task<SampleResult> CaptureCurrentAsync(
        TimeSpan duration,
        double sampleRateHz,
        Action<double>? onSample = null,
        Action<DateTime>? onCaptureStarted = null,
        CancellationToken ct = default,
        StabilitySpec? stability = null,
        TimeSpan delayBeforeCapture = default);
}

public interface IPowerSourceFactory
{
    /// <summary>列出本机可用的串口/VISA/模拟资源。</summary>
    IReadOnlyList<string> ListResources();

    /// <summary>新建一个未连接的实例。</summary>
    IPowerSource Create();
}
