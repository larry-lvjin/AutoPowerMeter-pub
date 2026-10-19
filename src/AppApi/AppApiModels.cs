using System.Text.Json.Serialization;

namespace AutoPowerMeter.AppApi;

public sealed record BatteryModeState
{
    [JsonPropertyName("requested")] public string? Requested { get; init; }
    [JsonPropertyName("effective")] public string? Effective { get; init; }
    [JsonPropertyName("deviceReported")] public string? DeviceReported { get; init; }
    [JsonPropertyName("derivedFromBatteryLevel")] public bool DerivedFromBatteryLevel { get; init; }
}

public sealed record AppDeviceState
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("sn")] public string? Sn { get; init; }
    [JsonPropertyName("connectedSn")] public string? ConnectedSn { get; init; }
    [JsonPropertyName("isBound")] public bool IsBound { get; init; }
    [JsonPropertyName("isConnected")] public bool IsConnected { get; init; }
    [JsonPropertyName("recordState")] public string RecordState { get; init; } = "stopped";
    [JsonPropertyName("isRecording")] public bool IsRecording { get; init; }
    [JsonPropertyName("isPaused")] public bool IsPaused { get; init; }
    [JsonPropertyName("deviceIsRecording")] public bool? DeviceIsRecording { get; init; }
    [JsonPropertyName("batteryLevel")] public int BatteryLevel { get; init; } = -1;
    [JsonPropertyName("isCharging")] public bool IsCharging { get; init; }
    [JsonPropertyName("findMy")] public int? FindMy { get; init; }
    [JsonPropertyName("findMyOn")] public bool? FindMyOn { get; init; }
    [JsonPropertyName("hasFindMyToken")] public bool? HasFindMyToken { get; init; }
    // v4 文档写为 0~3；当前 iOS 对内包也可能返回 "exists" 等字符串，保留原始 JSON 兼容两者。
    [JsonPropertyName("findMyTokenState")] public System.Text.Json.JsonElement? FindMyTokenState { get; init; }
    [JsonPropertyName("batteryMode")] public BatteryModeState? BatteryMode { get; init; }
    [JsonPropertyName("autoTransfer")] public AutoTransferState? AutoTransfer { get; init; }
    [JsonPropertyName("supportsBatteryMode")] public bool? SupportsBatteryMode { get; init; }
    [JsonPropertyName("supportsSmartMode")] public bool? SupportsSmartMode { get; init; }

    public string? EffectiveSn => ConnectedSn ?? Sn;

    // 断连后的 connectedSn 可能是旧缓存或空串；已绑定设备的 sn 才能作断连兜底。
    public string? BoundDeviceSn
    {
        get
        {
            if (!IsBound) return null;
            if (IsConnected && !string.IsNullOrWhiteSpace(ConnectedSn)) return ConnectedSn.Trim();
            return string.IsNullOrWhiteSpace(Sn) ? null : Sn.Trim();
        }
    }
}

public sealed record AutoTransferState
{
    // 设备未连接或能力尚未加载时，对内包会返回 null。
    [JsonPropertyName("supported")] public bool? Supported { get; init; }
    [JsonPropertyName("enabled")] public bool? Enabled { get; init; }
    [JsonPropertyName("locationAlwaysGranted")] public bool? LocationAlwaysGranted { get; init; }
}

public sealed record AppCommandResult(
    bool Changed,
    string? RecordState,
    bool BatteryModeSwitchReboots,
    string? Effective,
    string? DeviceReported);
