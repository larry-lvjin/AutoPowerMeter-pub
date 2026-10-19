namespace AutoPowerMeter.Models;

/// <summary>
/// 用例执行前需要达成的状态。State 字段为 Unknown 时表示"无所谓"。
/// </summary>
public sealed class Precondition
{
    public FindMyState FindMy { get; set; } = FindMyState.Unknown;
    public AppState App { get; set; } = AppState.Unknown;
    public RecordingMode Mode { get; set; } = RecordingMode.Unknown;
    public DeviceUiState DeviceUi { get; set; } = DeviceUiState.Unknown;

    /// <summary>电源电压 (V)。null 表示沿用当前。</summary>
    public double? VoltageV { get; set; }

    /// <summary>电流限制 (A)。null 表示沿用当前。</summary>
    public double? CurrentLimitA { get; set; }

    /// <summary>进入测量前的稳态等待秒数（例：测试待机功耗常需"1 分钟后"）。</summary>
    public int SettleSeconds { get; set; }

    /// <summary>本用例前需要切断再恢复电源。恢复后强制执行 3 秒预录音。</summary>
    public bool PowerCycle { get; set; }

    /// <summary>需要先做的额外人工动作描述（如"长按录音键开始录音"）。多条用 \n 分隔。</summary>
    public string? ManualPrompt { get; set; }
}
