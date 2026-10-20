using AutoPowerMeter.Models;

namespace AutoPowerMeter.Phone;

/// <summary>
/// 手机控制抽象。失败交给 StateMachine 记录、补全页面并重新检查，三次失败跳过当前用例。
/// </summary>
public interface IPhoneController
{
    bool IsConnected { get; }
    string? StatusText { get; }
    PhoneSettings Settings { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();

    /// <summary>
    /// 轻量读取当前手机界面，用于每个自动化行为执行前的监督。
    /// 只返回前台应用和少量可见控件摘要，不保存完整 WDA XML。
    /// </summary>
    Task<PhoneUiObservation> ObserveUiAsync(CancellationToken ct = default);
    Task<bool> IsScreenLockedAsync(CancellationToken ct = default);
    /// <summary>根据当前页面树执行一次安全的导航补全；由上层负责三次尝试和复核。</summary>
    Task RecoverUiAsync(PhoneUiTarget target, CancellationToken ct = default);
    /// <summary>页面树读取失败时，不依赖 /source，恢复 WDA 会话和系统导航；调用方必须重新读树验收。</summary>
    Task RecoverUiReadFailureAsync(PhoneUiTarget target, CancellationToken ct = default);
    Task<int> GetAppStateAsync(string bundleId, CancellationToken ct = default);

    /// <summary>用户点名的 6 个核心动作。</summary>
    Task AppKillAsync(CancellationToken ct = default);
    Task AppHomeAsync(CancellationToken ct = default);
    Task FindMyKillAsync(CancellationToken ct = default);
    Task FindMyHomeAsync(CancellationToken ct = default);
    Task FindMyUnbindAsync(CancellationToken ct = default);

    /// <summary>额外能纯软件做的：屏幕亮/灭、APP 切前台、模式切换。</summary>
    Task AppToScreenOnAsync(CancellationToken ct = default);
    Task AppToScreenOffAsync(CancellationToken ct = default);
    Task FindMyTurnOffAsync(CancellationToken ct = default);
    Task RunNamedActionAsync(string actionName, CancellationToken ct = default);

    /// <summary>调试用：取截图、原始 tap、刷新 tap_map。</summary>
    Task<byte[]> ScreenshotAsync(CancellationToken ct = default);
    Task TapRawAsync(double x, double y, CancellationToken ct = default);
    void ReloadTapMap();
}

public sealed record PhoneUiObservation(
    DateTimeOffset ObservedAt,
    string? ActiveBundleId,
    PhoneUiScene Scene,
    string PageSummary,
    IReadOnlyList<PhoneUiElement> Elements);

/// <summary>由 WDA 页面树识别出的稳定场景。Unknown 只表示尚无可靠特征，不据此盲点。</summary>
public enum PhoneUiScene
{
    Unknown,
    SpringBoard,
    PlaudHomeConnected,
    PlaudHomeDisconnected,
    PlaudDeviceList,
    PlaudDeviceDetail,
    PlaudConnectionManagement,
    PlaudRecording,
    PlaudOther,
    FindMyItemsList,
    FindMyItemDetail,
    FindMyAddItemSheet,
    FindMyPairing,
    FindMyOther,
    SettingsMain,
    SettingsBluetooth,
    SystemAlert
}

public enum PhoneUiTarget { Any, PlaudForeground, PlaudHome, FindMyDetail, FindMyItems, SpringBoard, Locked }

public static class PhoneUiEvidence
{
    // 新版录音面板使用“暂停/结束/录音标记”，而不是“暂停录音/结束录音/添加标记”。
    public static bool HasRecordingControls(IReadOnlyList<PhoneUiElement> elements)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }
}

/// <summary>WDA /source 中的一个可见元素；保留文字、状态和真实坐标供异常分支判断。</summary>
public sealed record PhoneUiElement(
    string Type,
    string Name,
    string Label,
    string Value,
    bool Enabled,
    double X,
    double Y,
    double Width,
    double Height)
{
    // 实现细节未公开（算法核心）
    throw new NotImplementedException("该方法的实现不在开源范围内");
}

public sealed class PhoneActionException : Exception
{
    public PhoneActionException(string message, Exception? inner = null) : base(message, inner) { }
}
