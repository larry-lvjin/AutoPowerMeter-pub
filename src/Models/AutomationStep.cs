namespace AutoPowerMeter.Models;

public sealed class AutomationStep
{
    public AutomationActionKind Action { get; set; }
    public int DurationSeconds { get; set; }
    public string Description { get; set; } = "";

    public override string ToString()
        => DurationSeconds > 0 ? $"{Action} ({DurationSeconds}s)" : Action.ToString();
}
