namespace AutoPowerMeter.Phone;

/// <summary>iPhone 自动化相关配置。可在 GUI 编辑，持久化到 tap_map.json。</summary>
public sealed class PhoneSettings
{
    public string WdaBaseUrl { get; set; } = "http://127.0.0.1:8100";
    public string AppApiBaseUrl { get; set; } = "http://127.0.0.1:8090";
    public string PlaudBundleId { get; set; } = "ai.plaud.ios.plaud";
    public string FindMyBundleId { get; set; } = "com.apple.findmy";
    public string DvtCliPath { get; set; } = @"D:\PythonTools\pymobiledevice3\Scripts\pymobiledevice3.exe";
    /// <summary>留空时自动选择唯一USB手机；多手机时使用已配置的UDID。</summary>
    public string IosUdid { get; set; } = "";
    /// <summary>Android HTTP 单独转发时的设备序列号；留空仅在唯一设备连接时自动选择。</summary>
    public string AndroidSerial { get; set; } = "";
    public string WdaRunnerBundleId { get; set; } = "com.larryautopower.WebDriverAgentRunner.xctrunner";
    public string WdaTestConfiguration { get; set; } = "WebDriverAgentRunner.xctest";

    /// <summary>WDA 录制 JSON 目录。相对路径按程序目录解析。</summary>
    public string RecordingDirectory { get; set; } = "Resources/recordings";

    /// <summary>启用后优先运行手机快捷指令；未到达目标页面时自动使用 WDA 原生动作。</summary>
    public bool UseShortcutsForNavigation { get; set; }
    public string ShortcutGoHome { get; set; } = "APM-回桌面";
    public string ShortcutOpenPlaud { get; set; } = "APM-打开PLAUD";
    public string ShortcutOpenFindMy { get; set; } = "APM-打开查找";
    public string ShortcutLockPlaud { get; set; } = "APM-PLAUD灭屏";
    public string ShortcutLockFindMy { get; set; } = "APM-查找灭屏";
    public string ShortcutBluetoothOff { get; set; } = "APM-关闭蓝牙";
    public string ShortcutBluetoothOn { get; set; } = "APM-打开蓝牙";
    /// <summary>旧的关闭再打开蓝牙动作保持停用；新 Excel 明确的单向开/关快捷指令不受此开关影响。</summary>
    public bool EnableBluetoothAutomation { get; set; }

    /// <summary>动作执行间隔（默认 800ms，给 UI 留过渡时间）。</summary>
    public int DefaultStepDelayMs { get; set; } = 800;
}
