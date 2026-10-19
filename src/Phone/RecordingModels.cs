using System.Text.Json.Serialization;

namespace AutoPowerMeter.Phone;

public sealed record WdaRecording
{
    [JsonPropertyName("created")] public string Created { get; init; } = "";
    [JsonPropertyName("device")] public WdaRecordingDevice Device { get; init; } = new();
    [JsonPropertyName("total_duration")] public double TotalDuration { get; init; }
    [JsonPropertyName("action_count")] public int ActionCount { get; init; }
    [JsonPropertyName("precondition")] public WdaRecordingPrecondition? Precondition { get; init; }
    [JsonPropertyName("expected_end_state")] public string? ExpectedEndState { get; init; }
    [JsonPropertyName("actions")] public List<WdaRecordedAction> Actions { get; init; } = new();
}

public sealed record WdaRecordingPrecondition
{
    [JsonPropertyName("scene")] public string Scene { get; init; } = "";
    [JsonPropertyName("bundle_id")] public string? BundleId { get; init; }
    [JsonPropertyName("activate_app")] public bool ActivateApp { get; init; }
    [JsonPropertyName("required_accessibility_ids")] public List<string> RequiredAccessibilityIds { get; init; } = new();
    [JsonPropertyName("back_accessibility_id")] public string? BackAccessibilityId { get; init; }
    [JsonPropertyName("recovery_accessibility_ids")] public List<string> RecoveryAccessibilityIds { get; init; } = new();
    [JsonPropertyName("max_back_steps")] public int MaxBackSteps { get; init; }
    [JsonPropertyName("required_app_state")] public string? RequiredAppState { get; init; }
}

public sealed record WdaRecordingDevice
{
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
}

public sealed record WdaRecordedAction
{
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("t")] public double T { get; init; }
    [JsonPropertyName("x")] public double? X { get; init; }
    [JsonPropertyName("y")] public double? Y { get; init; }
    [JsonPropertyName("x_from")] public double? XFrom { get; init; }
    [JsonPropertyName("x_to")] public double? XTo { get; init; }
    [JsonPropertyName("y_from")] public double? YFrom { get; init; }
    [JsonPropertyName("y_to")] public double? YTo { get; init; }
    [JsonPropertyName("from")] public double[]? From { get; init; }
    [JsonPropertyName("to")] public double[]? To { get; init; }
    [JsonPropertyName("duration")] public double? Duration { get; init; }
    [JsonPropertyName("move_ms")] public int? MoveMs { get; init; }
    [JsonPropertyName("times")] public int? Times { get; init; }
    [JsonPropertyName("seconds")] public double? Seconds { get; init; }
    [JsonPropertyName("enabled")] public bool? Enabled { get; init; }
    [JsonPropertyName("bundle_id")] public string? BundleId { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("file")] public string? File { get; init; }
    [JsonPropertyName("accessibility_id")] public string? AccessibilityId { get; init; }
    [JsonPropertyName("xpath")] public string? XPath { get; init; }
    [JsonPropertyName("timeout_seconds")] public double? TimeoutSeconds { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("avoid_bottom_overlay")] public bool AvoidBottomOverlay { get; init; }
    [JsonPropertyName("max_safe_y")] public double? MaxSafeY { get; init; }
    /// <summary>动作执行前必须在当前可见页面树中出现；用于防止相同坐标在错误页面被点击。</summary>
    [JsonPropertyName("required_before_accessibility_ids")]
    public List<string> RequiredBeforeAccessibilityIds { get; init; } = new();
    /// <summary>动作执行后必须在时限内出现；用于确认已经进入预期的下一场景。</summary>
    [JsonPropertyName("expected_after_accessibility_ids")]
    public List<string> ExpectedAfterAccessibilityIds { get; init; } = new();
}
