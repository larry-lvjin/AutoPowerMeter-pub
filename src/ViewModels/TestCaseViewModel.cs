using AutoPowerMeter.Models;
using AutoPowerMeter.Utils;

namespace AutoPowerMeter.ViewModels;

/// <summary>测试计划列表里一行的可绑定视图。</summary>
public sealed class TestCaseViewModel : ObservableObject
{
    public TestCase Source { get; }

    public TestCaseViewModel(TestCase tc)
    {
        Source = tc;
        _isSelected = false;
    }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, Source.ExcludedByUser ? false : value); }
    public bool CanSelect => !Source.ExcludedByUser;

    public string Id => Source.Id;
    public int ExcelRow => Source.ExcelRow;
    public int ExecutionOrder => Source.ExecutionOrder;
    public string Category => Source.Category;
    public string ProjectName => Source.ProjectName;
    public string ProjectDisplay => $"【{Source.MeasurementKind}】 {Source.ProjectName}";
    public string CategoryProjectDisplay => string.Join("\n", new[]
    {
        Source.Category,
        Source.ProjectName
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
    /// <summary>
    /// 用例队列按 Excel 模板的原始层次完整显示：测试类别、测试项目、操作步骤。
    /// 类别和项目在生成计划时已按合并单元格向下继承，因此像 T195
    /// 这种 Excel 中项目单元格为空的行，仍会显示“蜂鸣器”。
    /// </summary>
    public string FullCaseDisplay => string.Join("\n", new[]
    {
        Source.Category,
        Source.ProjectName,
        Source.Description
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public string MeasurementKind => Source.MeasurementKind;
    public string Description => Source.Description;
    public string DisplayTitle => Source.DisplayTitle;
    public string LimitText => Source.LimitText;
    public AutomationLevel AutomationLevel => Source.AutomationLevel;
    public string StepsSummary => string.Join(" → ", Source.Steps.Select(x => x.ToString()));
    public string ParseNotes => string.Join("；", Source.ParseNotes);

    public string AutomationLabel => AutomationLevel switch
    {
        AutomationLevel.Auto => "全自动",
        AutomationLevel.SemiAuto => "半自动",
        AutomationLevel.Manual => "人工",
        AutomationLevel.Cancelled when Source.ExcludedByUser => "已剔除",
        AutomationLevel.Cancelled => "已取消",
        _ => "?"
    };

    private double _measuredMa = double.NaN;
    public double MeasuredMa
    {
        get => _measuredMa;
        set { if (SetProperty(ref _measuredMa, value)) OnPropertyChanged(nameof(MeasuredDisplay)); }
    }
    public string MeasuredDisplay => UnitFormatter.FormatCurrentMa(_measuredMa);

    private ResultStatus _status = ResultStatus.Pending;
    public ResultStatus Status
    {
        get => _status;
        set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusLabel)); }
    }
    public string StatusLabel => Status switch
    {
        ResultStatus.Pending => "待跑",
        ResultStatus.Pass => "Pass",
        ResultStatus.Warning => "警告",
        ResultStatus.Fail => "Fail",
        ResultStatus.Skipped when Source.ExcludedByUser => "已剔除",
        ResultStatus.Skipped when AutomationLevel is AutomationLevel.Manual or AutomationLevel.Cancelled => "需手动",
        ResultStatus.Skipped => "跳过",
        ResultStatus.Error => "错误",
        _ => ""
    };

    private string _note = "";
    public string Note { get => _note; set => SetProperty(ref _note, value); }

    private DateTime? _measurementStartedAt;
    public DateTime? MeasurementStartedAt
    {
        get => _measurementStartedAt;
        set => SetProperty(ref _measurementStartedAt, value);
    }

    private DateTime? _actionsFinishedAt;
    public DateTime? ActionsFinishedAt
    {
        get => _actionsFinishedAt;
        set => SetProperty(ref _actionsFinishedAt, value);
    }

    private double? _actualMeasurementDurationSeconds;
    public double? ActualMeasurementDurationSeconds
    {
        get => _actualMeasurementDurationSeconds;
        set => SetProperty(ref _actualMeasurementDurationSeconds, value);
    }

    private string? _rawDataPath;
    public string? RawDataPath { get => _rawDataPath; set => SetProperty(ref _rawDataPath, value); }
}
