namespace AutoPowerMeter.Models;

public enum FindMyState
{
    Unknown,
    /// <summary>FindMy 未绑定，初始广播状态。</summary>
    Unbound,
    /// <summary>FindMy 已绑定到 iCloud。</summary>
    Bound,
    /// <summary>FindMy APP 已被杀掉（绑定关系仍在）。</summary>
    Killed,
    /// <summary>iPhone 设置里关掉了 FindMy 功能。</summary>
    Off
}

public enum AppState
{
    Unknown,
    /// <summary>不连 APP（APP 未启动或未与设备配对）。</summary>
    NotConnected,
    /// <summary>APP 在前台，手机亮屏，停在 APP 界面。</summary>
    ForegroundScreenOn,
    /// <summary>APP 在前台但手机灭屏。</summary>
    ForegroundScreenOff,
    /// <summary>APP 后台、手机回到桌面。</summary>
    BackgroundHome,
    /// <summary>APP 被杀掉。</summary>
    Killed,
    /// <summary>APP 与设备已解绑。</summary>
    Unbound
}

public enum RecordingMode
{
    Unknown,
    HighPerformance,
    LongLife,
    Adaptive
}

public enum DeviceUiState
{
    Unknown,
    ScreenOn,
    Standby,
    Recording,
    Charging,
    Shutdown
}

public enum AutomationLevel
{
    /// <summary>完全自动（不需人工介入）。</summary>
    Auto,
    /// <summary>半自动（需要一次性人工绑定/解绑，状态机间自动转移）。</summary>
    SemiAuto,
    /// <summary>必须人工（录音键长按、充电线、xlsx 标注"需手动操作"等）。</summary>
    Manual,
    /// <summary>按用户要求取消：需要人工、物理工装或外部条件的项目不进入执行。</summary>
    Cancelled
}

public enum AutomationActionKind
{
    EnsureAppConnected,
    SetFindMyOn,
    SetFindMyOff,
    SetModeHighPerformance,
    SetModeLongLife,
    SetModeAdaptive,
    StartRecording,
    PauseRecording,
    ResumeRecording,
    StopRecording,
    RecordWithSoftwareMark,
    SetAutoTransferOn,
    SetAutoTransferOff,
    AppForeground,
    AppHome,
    AppScreenOff,
    AppKill,
    AppUnbind,
    DisconnectAppDevice,
    ReconnectAppDevice,
    BluetoothOffOn,
    FindMyBind,
    FindMyUnbind,
    FindMyKill,
    FindMyPlaySound,
    FindMyStopSound,
    FindMyHome,
    FindMyScreenOff,
    DeviceScreenOn,
    Wait,
    Manual,
    AddRecordingMark,
    FindMyScreenOn,
    FindMyOpen,
    BluetoothOff,
    BluetoothOn
}

public enum MeasurementType
{
    /// <summary>瞬时单次测量。</summary>
    Instant,
    /// <summary>时间区间内平均值。</summary>
    Average,
    /// <summary>时间区间内峰值（用于开机瞬间）。</summary>
    Peak
}

public enum CurrentRange
{
    /// <summary>自动选择（驱动内部决策）。</summary>
    Auto,
    /// <summary>低量程（µA 级精测，66319D 满量程约 20mA）。</summary>
    Low,
    /// <summary>高量程（mA/A 级，开机瞬间峰值用，~3A）。</summary>
    High
}

public enum LimitOp
{
    /// <summary>≤</summary>
    LessOrEqual,
    /// <summary>&lt;</summary>
    Less,
    /// <summary>≥</summary>
    GreaterOrEqual,
    /// <summary>&gt;</summary>
    Greater
}

public enum ResultStatus
{
    Pending,
    Pass,
    /// <summary>在容差区间内（如 320µA±40µA 实测落到 320–360 之间）。</summary>
    Warning,
    Fail,
    Skipped,
    Error
}
