namespace AutoPowerMeter.Models;

/// <summary>单个自动化动作的时间记录，用于把测试结果精确映射到波形。</summary>
public sealed record ActionTiming(
    string Action,
    string Description,
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Succeeded,
    string? Error);
