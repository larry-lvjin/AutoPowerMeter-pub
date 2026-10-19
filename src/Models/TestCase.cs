using System.Text.Json.Serialization;

namespace AutoPowerMeter.Models;

/// <summary>
/// 一条测试用例：对应 xlsx 中一行 + 解析后的可执行字段。
/// </summary>
public sealed class TestCase
{
    public string Id { get; set; } = "";

    /// <summary>A 列：测试类别。</summary>
    public string Category { get; set; } = "";

    /// <summary>B 列：测试项目。</summary>
    public string ProjectName { get; set; } = "";

    /// <summary>C/D 列：详述（自然语言）。</summary>
    public string Description { get; set; } = "";

    /// <summary>G 列：参考标准原文。</summary>
    public string LimitText { get; set; } = "";

    /// <summary>xlsx 行号（1-based, 含表头）。</summary>
    public int ExcelRow { get; set; }

    /// <summary>按相同设备场景聚合后的执行顺序。</summary>
    public int ExecutionOrder { get; set; }
    public bool AuthoritativeSequence { get; set; }
    public string SequenceSource { get; set; } = "";
    public int SequenceSourceRow { get; set; }
    public string PreviousSequenceCaseId { get; set; } = "";
    public List<string> TransitionDescriptions { get; set; } = new();
    public List<int> SharedResultRows { get; set; } = new();
    /// <summary>增量步骤没有录音动作时仍继承上一项的录音/暂停真值。</summary>
    public string? ExpectedRecordState { get; set; }
    public string ResultReuseFromCaseId { get; set; } = "";
    /// <summary>用户明确要求从自动测试和报告中剔除的用例。</summary>
    public bool ExcludedByUser { get; set; }
    public string ScenarioId { get; set; } = "";
    /// <summary>紧邻录音项成功且会话仍活动时，只停止该会话并测待机；否则执行独立准备。</summary>
    public string ReuseRecordingFromCaseId { get; set; } = "";
    /// <summary>紧邻来源项成功且 WDA 页面仍符合时，复用其事件结果，仅执行新增场景动作。</summary>
    public string ReuseSceneFromCaseId { get; set; } = "";
    public List<AutomationActionKind> ReusedSceneActions { get; set; } = new();
    public string InitialScene { get; set; } = "";
    public string SceneOperation { get; set; } = "";
    public string MeasurementAction { get; set; } = "";
    public string AutomationChannel { get; set; } = "";
    public string RecordingScript { get; set; } = "";

    public AutomationLevel AutomationLevel { get; set; } = AutomationLevel.Manual;

    public Precondition Precondition { get; set; } = new();

    public MeasurementSpec Measurement { get; set; } = new();

    public Limit Limit { get; set; } = new();

    /// <summary>按顺序执行的 APP HTTP / WDA 动作。测流前统一再等待 SettleSeconds。</summary>
    public List<AutomationStep> Steps { get; set; } = new();

    /// <summary>解析过程中产生的提示/警告（如关键字未匹配）。</summary>
    public List<string> ParseNotes { get; set; } = new();

    [JsonIgnore]
    public string DisplayTitle =>
        string.IsNullOrEmpty(ProjectName) ? Description : $"{ProjectName} | {Description}";

    [JsonIgnore]
    public string MeasurementKind
    {
        get
        {
            if (ExpectedRecordState == "recording") return "录音电流";
            if (ExpectedRecordState == "paused") return "暂停录音电流";
            var text = $"{ProjectName}\n{Description}";
            if (text.Contains("停止录音后", StringComparison.OrdinalIgnoreCase)
                || text.Contains("录音结束后", StringComparison.OrdinalIgnoreCase)
                || text.Contains("结束录音后", StringComparison.OrdinalIgnoreCase)
                || text.Contains("回归", StringComparison.OrdinalIgnoreCase)
                || text.Contains("待机", StringComparison.OrdinalIgnoreCase))
                return "待机电流";
            if (text.Contains("录音平均电流", StringComparison.OrdinalIgnoreCase)
                || Steps.Any(x => x.Action is AutomationActionKind.StartRecording
                    or AutomationActionKind.ResumeRecording or AutomationActionKind.RecordWithSoftwareMark))
                return Steps.Any(x => x.Action == AutomationActionKind.PauseRecording)
                    ? "暂停录音电流" : "录音电流";
            return Measurement.Type == MeasurementType.Peak ? "峰值电流" : "平均电流";
        }
    }
}
