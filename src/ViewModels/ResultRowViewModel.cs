using AutoPowerMeter.Models;
using AutoPowerMeter.Utils;

namespace AutoPowerMeter.ViewModels;

public sealed class ResultRowViewModel : ObservableObject
{
    public string Id { get; }
    public int ExcelRow { get; }
    public string Category { get; }
    public string ProjectName { get; }
    public string LimitText { get; }

    private double _measuredMa;
    public double MeasuredMa
    {
        get => _measuredMa;
        set { if (SetProperty(ref _measuredMa, value)) OnPropertyChanged(nameof(MeasuredDisplay)); }
    }
    public string MeasuredDisplay => UnitFormatter.FormatCurrentMa(_measuredMa);

    private ResultStatus _status;
    public ResultStatus Status
    {
        get => _status;
        set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusLabel)); }
    }
    public string StatusLabel => Status switch
    {
        ResultStatus.Pass => "Pass",
        ResultStatus.Warning => "警告",
        ResultStatus.Fail => "Fail",
        ResultStatus.Skipped => "跳过",
        ResultStatus.Error => "错误",
        _ => "—"
    };

    private DateTime _finishedAt;
    public DateTime FinishedAt { get => _finishedAt; set => SetProperty(ref _finishedAt, value); }

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

    private string _note = "";
    public string Note { get => _note; set => SetProperty(ref _note, value); }

    private string _actionTimeline = "";
    public string ActionTimeline { get => _actionTimeline; set => SetProperty(ref _actionTimeline, value); }

    public ResultRowViewModel(TestCase tc, RunResult r)
    {
        Id = tc.Id;
        ExcelRow = tc.ExcelRow;
        Category = tc.Category;
        ProjectName = tc.ProjectName;
        LimitText = tc.LimitText;
        Update(r);
    }

    public void Update(RunResult r)
    {
        MeasuredMa = r.MeasuredMa;
        Status = r.Status;
        FinishedAt = r.FinishedAt;
        MeasurementStartedAt = r.MeasurementStartedAt;
        ActionsFinishedAt = r.ActionsFinishedAt;
        ActualMeasurementDurationSeconds = r.ActualMeasurementDurationSeconds;
        RawDataPath = r.RawDataPath;
        Note = r.Note;
        ActionTimeline = string.Join(" | ", r.ActionTimings.Select(x =>
            $"{x.Action}:{x.StartedAt:HH:mm:ss.fff}-{x.FinishedAt:HH:mm:ss.fff}"));
    }
}
