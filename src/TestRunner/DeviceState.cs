using AutoPowerMeter.Models;

namespace AutoPowerMeter.TestRunner;

/// <summary>StateMachine 内部维护的当前世界状态。</summary>
public sealed class DeviceState
{
    public FindMyState FindMy { get; set; } = FindMyState.Unknown;
    public AppState App { get; set; } = AppState.Unknown;
    public RecordingMode Mode { get; set; } = RecordingMode.Unknown;
    public DeviceUiState DeviceUi { get; set; } = DeviceUiState.Unknown;
    public double? VoltageV { get; set; }
    public double? CurrentLimitA { get; set; }
    public bool OutputOn { get; set; }

    public DeviceState Clone() => new()
    {
        FindMy = FindMy, App = App, Mode = Mode, DeviceUi = DeviceUi,
        VoltageV = VoltageV, CurrentLimitA = CurrentLimitA, OutputOn = OutputOn
    };
}
