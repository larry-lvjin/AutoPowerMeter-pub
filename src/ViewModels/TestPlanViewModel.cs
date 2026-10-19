using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using AutoPowerMeter.Excel;
using AutoPowerMeter.Instruments;
using AutoPowerMeter.Models;
using AutoPowerMeter.Phone;
using AutoPowerMeter.TestRunner;
using AutoPowerMeter.Utils;
using Microsoft.Extensions.Logging;

namespace AutoPowerMeter.ViewModels;

public sealed class TestPlanViewModel : ObservableObject
{
    private readonly ConnectViewModel _connect;
    private readonly PhoneViewModel _phone;
    private readonly ResultsViewModel _results;
    private readonly ILogger<TestPlanViewModel> _logger;
    private readonly ManualPromptService _prompt = new();

    private WorkbookReader.WorkbookData? _workbook;
    private WorkbookWriter? _writer;
    private TestEngine? _engine;
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _setupCts;
    private CancellationTokenSource? _caseCts;
    private bool _pauseRequested;

    public ObservableCollection<TestCaseViewModel> Cases { get; } = new();
    public IEnumerable<TestCaseViewModel> VisibleCases => Cases.Where(item =>
        item.AutomationLevel is AutomationLevel.Auto or AutomationLevel.SemiAuto);
    public ObservableCollection<TestCaseGroupViewModel> CaseGroups { get; } = new();
    public ObservableCollection<TestSuiteOption> TestSuites { get; } = new();

    private TestSuiteOption? _selectedSuite;
    public TestSuiteOption? SelectedSuite
    {
        get => _selectedSuite;
        set
        {
            if (!SetProperty(ref _selectedSuite, value) || value is null) return;

            _writer?.Dispose();
            _writer = null;
            ExportReportCommand.RaiseCanExecuteChanged();
            _workbook = null;
            SelectedCase = null;
            XlsxPath = value.FilePath;
            LoadPlan();
        }
    }

    private bool? _selectAll = false;
    private bool _applyingSelectAll;
    public bool? SelectAll
    {
        get => _selectAll;
        set
        {
            // null 仅用于显示“部分选择”；用户点击表头只执行全选或全不选。
            if (value is null) return;
            if (!SetProperty(ref _selectAll, value)) return;
            _applyingSelectAll = true;
            try
            {
                foreach (var item in VisibleCases) item.IsSelected = value.Value;
            }
            finally { _applyingSelectAll = false; }
            OnPropertyChanged(nameof(SelectionSummary));
            RunCheckedCommand.RaiseCanExecuteChanged();
        }
    }
    public string SelectionSummary
    {
        get
        {
            var visible = VisibleCases.ToArray();
            return $"已选 {visible.Count(x => x.IsSelected)} / {visible.Length} 条";
        }
    }

    private string _xlsxPath = @"D:\MyFile\CODE\VSCODE\AutoPowerMeter\Docs\模板Delta S_MB_P0.1-V1.0.0 功耗测试深联+三德盈PCB+M8(RZ+M8) (1).xlsx";
    public string XlsxPath { get => _xlsxPath; set => SetProperty(ref _xlsxPath, value); }

    private string _deviceSerial = "8831B30314393233";
    public string DeviceSerial { get => _deviceSerial; set => SetProperty(ref _deviceSerial, value); }

    private string _statusText = "未加载";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private string? _outputDirectoryError;
    public string FailWaveformDirectory
    {
        get => ServiceLocator.Current.OutputStorage.DirectoryPath;
        set
        {
            try
            {
                ServiceLocator.Current.OutputStorage.ChangeDirectory(value, _writer);
                _outputDirectoryError = null;
                OnPropertyChanged();
                ExportReportCommand.RaiseCanExecuteChanged();
                _logger.LogInformation("输出目录已同步：{directory}（日志、Excel报告、FAIL波形）", FailWaveformDirectory);
            }
            catch (Exception ex) { _outputDirectoryError = ex.Message; throw; }
        }
    }

    private TestCaseViewModel? _selectedCase;
    public TestCaseViewModel? SelectedCase
    {
        get => _selectedCase;
        set => SetProperty(ref _selectedCase, value);
    }

    private TestCaseViewModel? _runningCase;
    public TestCaseViewModel? RunningCase
    {
        get => _runningCase;
        private set
        {
            if (SetProperty(ref _runningCase, value))
                OnPropertyChanged(nameof(RunningCaseTitle));
        }
    }
    public string RunningCaseTitle => _runningCase is null
        ? "—"
        : NormalizeRunningCaseTitle(
            $"{_runningCase.Id} {_runningCase.Category} {_runningCase.ProjectName}");

    private static string NormalizeRunningCaseTitle(string title) =>
        Regex.Replace(title.Replace('(', '（').Replace(')', '）'), @"\s+", " ").Trim();

    private int _remainingSeconds;
    public int RemainingSeconds { get => _remainingSeconds; private set => SetProperty(ref _remainingSeconds, value); }

    private double _liveMa = double.NaN;
    public double LiveMa
    {
        get => _liveMa;
        private set
        {
            if (SetProperty(ref _liveMa, value)) OnPropertyChanged(nameof(LiveDisplay));
        }
    }
    public string LiveDisplay => UnitFormatter.FormatCurrentMa(_liveMa);

    public AsyncRelayCommand RunCheckedCommand { get; }
    public AsyncRelayCommand PrepareAndroidHttpCommand { get; }
    public RelayCommand ExportReportCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand SkipCommand { get; }
    public RelayCommand BrowseFailWaveformDirectoryCommand { get; }

    public bool IsPaused
    {
        get => _pauseRequested;
        private set
        {
            if (SetProperty(ref _pauseRequested, value))
                OnPropertyChanged(nameof(PauseButtonText));
        }
    }
    public string PauseButtonText => IsPaused ? "继续" : "暂停";

    public TestPlanViewModel(ConnectViewModel connect, PhoneViewModel phone, ResultsViewModel results,
        string? testPlanPath = null)
    {
        _connect = connect;
        _phone = phone;
        _results = results;
        _logger = ServiceLocator.Current.LoggerFactory.CreateLogger<TestPlanViewModel>();

        RunCheckedCommand = new AsyncRelayCommand(RunCheckedAsync,
            () => VisibleCases.Any(x => x.IsSelected) && _runCts is null && _setupCts is null);
        PrepareAndroidHttpCommand = new AsyncRelayCommand(PrepareAndroidHttpAsync,
            () => _runCts is null && _setupCts is null);
        ExportReportCommand = new RelayCommand(ExportReport,
            () => _writer is not null && File.Exists(_writer.FilePath));
        StopCommand = new RelayCommand(Stop, () => _runCts is not null || _setupCts is not null);
        PauseCommand = new RelayCommand(() => IsPaused = !IsPaused, () => _runCts is not null);
        SkipCommand = new RelayCommand(() => _caseCts?.Cancel(), () => _caseCts is not null);
        BrowseFailWaveformDirectoryCommand = new RelayCommand(BrowseFailWaveformDirectory);

        _connect.PropertyChanged += (_, e) =>
        {
            if (_setupCts is not null && e.PropertyName == nameof(ConnectViewModel.StatusText))
                StatusText = _connect.StatusText;
        };
        _phone.PropertyChanged += (_, e) =>
        {
            if (_setupCts is not null && e.PropertyName == nameof(PhoneViewModel.StatusText))
                StatusText = _phone.StatusText;
        };

        if (testPlanPath is null)
            DiscoverTestSuites();
        else
        {
            var suite = new TestSuiteOption(CreateSuiteDisplayName(testPlanPath), testPlanPath,
                ResolveReportTemplatePath(testPlanPath));
            TestSuites.Add(suite);
            SelectedSuite = suite;
        }
    }

    private void DiscoverTestSuites()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            for (var depth = 0; directory is not null && depth < 8; depth++, directory = directory.Parent)
            {
                var docsPath = Path.Combine(directory.FullName, "Docs");
                if (!Directory.Exists(docsPath)) continue;

                foreach (var path in Directory.EnumerateFiles(docsPath, "模板*.xlsx", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(path);
                    if (!fileName.StartsWith("~$", StringComparison.Ordinal) &&
                        !fileName.Contains("生成数据", StringComparison.Ordinal))
                        paths.Add(Path.GetFullPath(path));
                }
                if (paths.Count > 0) break;
            }
            if (paths.Count > 0) break;
        }

        foreach (var path in paths
                     .OrderBy(path => Path.GetFileName(path).Contains("Delta", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            if (AuthoritativeDeltaPlan.IsDelta(path))
            {
                var suiteDirectory = Path.Combine(Path.GetDirectoryName(path)!, "测试用例及模板");
                foreach (var kind in new[] { "全功耗", "基本功耗" })
                {
                    var sequence = Path.Combine(suiteDirectory, $"Deltas + Delta {kind}  测试用例.xlsx");
                    var report = Path.Combine(suiteDirectory, $"Deltas + Delta {kind}  测试报告模板.xlsx");
                    if (!File.Exists(sequence) || !File.Exists(report)) continue;
                    foreach (var variant in new[] { "Deltas", "Delta" })
                    {
                        var delta = variant == "Delta";
                        TestSuites.Add(new TestSuiteOption($"{variant} Link iOS {kind}", report, report,
                            sequence, sequence, kind == "全功耗" ? (delta ? 7 : 6) : (delta ? 9 : 8),
                            delta ? 8 : 7, 9));
                    }
                }
                continue;
            }
            TestSuites.Add(new TestSuiteOption(
                CreateSuiteDisplayName(path),
                path,
                ResolveReportTemplatePath(path)));
        }

        SelectedSuite = TestSuites.FirstOrDefault(suite =>
                            suite.DisplayName == "Deltas Link iOS 全功耗")
                        ?? TestSuites.FirstOrDefault();

        if (SelectedSuite is null)
            StatusText = "未在 Docs 目录发现“模板*.xlsx”测试方案";
    }

    private static string CreateSuiteDisplayName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (AuthoritativeDeltaPlan.IsDelta(path))
            return name.StartsWith("Delta 全功耗", StringComparison.OrdinalIgnoreCase)
                ? "Delta Link iOS 全功耗" : "Deltas Link iOS 全功耗";
        var family = Regex.Match(name, @"^模板\s*(Delta|Sigma)\b", RegexOptions.IgnoreCase).Groups[1].Value;
        if (string.IsNullOrWhiteSpace(family))
            family = name.StartsWith("模板", StringComparison.Ordinal) ? name[2..].Split(' ', 2)[0] : name;

        var version = Regex.Match(name, @"S_[^\s]+", RegexOptions.IgnoreCase).Value;
        return string.IsNullOrWhiteSpace(version)
            ? $"{family} · 整套功耗测试"
            : $"{family} · {version}";
    }

    private static string ResolveReportTemplatePath(string testPlanPath)
    {
        if (AuthoritativeDeltaPlan.IsDelta(testPlanPath))
        {
            var directory = Path.GetDirectoryName(testPlanPath)!;
            var suiteDirectory = Path.GetFileName(directory).Equals("测试用例及模板", StringComparison.OrdinalIgnoreCase)
                ? directory : Path.Combine(directory, "测试用例及模板");
            var variant = Path.GetFileNameWithoutExtension(testPlanPath)
                .StartsWith("Delta 全功耗", StringComparison.OrdinalIgnoreCase) ? "Delta" : "Deltas";
            var deltaTemplate = Path.Combine(suiteDirectory, $"{variant} 全功耗 生成数据模板.xlsx");
            if (!File.Exists(deltaTemplate)) throw new FileNotFoundException("缺少Delta生成数据模板", deltaTemplate);
            return deltaTemplate;
        }
        var reportTemplate = Path.Combine(Path.GetDirectoryName(testPlanPath)!, "导出报告模板.xlsx");
        return File.Exists(reportTemplate) ? reportTemplate : testPlanPath;
    }

    private void UpdateRunCommandStates()
    {
        RunCheckedCommand.RaiseCanExecuteChanged();
        PrepareAndroidHttpCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        SkipCommand.RaiseCanExecuteChanged();
    }

    private async Task PrepareAndroidHttpAsync()
    {
        try
        {
            StatusText = "正在检查 Android USB 设备与 8090 转发…";
            var settings = TapMap.LoadOrDefault(ServiceLocator.Current.TapMapPath).Settings;
            var serial = await new AndroidHttpForwarder(new AdbForwardHost())
                .EnsureAsync(preferredSerial: settings.AndroidSerial, repair: true).ConfigureAwait(true);
            StatusText = $"Android {serial} 的 HTTP 8090 转发已就绪";
            _logger.LogInformation("{message}", StatusText);
        }
        catch (Exception ex)
        {
            StatusText = "Android HTTP 转发失败：" + ex.Message;
            _logger.LogError(ex, "Android USB HTTP 转发失败");
            MessageBox.Show(StatusText, "Android HTTP 转发", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadPlan(IReadOnlySet<string>? restoreSelection = null)
    {
        try
        {
            if (!File.Exists(XlsxPath))
                throw new FileNotFoundException("测试计划 xlsx 不存在", XlsxPath);
            var reader = new WorkbookReader(ServiceLocator.Current.LoggerFactory.CreateLogger<WorkbookReader>());
            var workbook = reader.Read(XlsxPath, DeviceSerial);
            var gen = new TestPlanGenerator(ServiceLocator.Current.LoggerFactory.CreateLogger<TestPlanGenerator>());
            var list = gen.Generate(workbook, SelectedSuite?.SequencePath, SelectedSuite?.StandardsPath,
                SelectedSuite?.StandardsColumnIndex ?? 6);
            if (restoreSelection is not null && list.Count(tc => restoreSelection.Contains(tc.Id)
                    && tc.AutomationLevel is AutomationLevel.Auto or AutomationLevel.SemiAuto) != restoreSelection.Count)
                throw new InvalidOperationException("读取样机 SN 后，已勾选用例无法完整保留；本轮未开始测试");

            _workbook = workbook;
            Cases.Clear();
            foreach (var tc in list)
            {
                var item = new TestCaseViewModel(tc);
                if (restoreSelection?.Contains(tc.Id) == true) item.IsSelected = true;
                Cases.Add(item);
            }
            OnPropertyChanged(nameof(VisibleCases));
            CaseGroups.Clear();
            foreach (var group in Cases.GroupBy(x => x.Category))
                CaseGroups.Add(new TestCaseGroupViewModel(group.Key, group));
            foreach (var item in Cases)
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TestCaseViewModel.IsSelected))
                    {
                        if (_applyingSelectAll) return;
                        RefreshSelectAllState();
                        OnPropertyChanged(nameof(SelectionSummary));
                        RunCheckedCommand.RaiseCanExecuteChanged();
                    }
                };
            RefreshSelectAllState();
            OnPropertyChanged(nameof(SelectionSummary));
            StatusText = list.Any(t => t.AuthoritativeSequence)
                ? $"{SelectedSuite?.DisplayName ?? "Delta"} · 自由勾选 {list.Count(t => t.AutomationLevel == AutomationLevel.Auto)} 项；按勾选项执行"
                : $"已加载 {Cases.Count} 条用例";
            RunCheckedCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "load plan failed");
            if (restoreSelection is not null) throw;
            MessageBox.Show(ex.ToString(), "加载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshSelectAllState()
    {
        var visible = VisibleCases.ToArray();
        bool? state = visible.Length == 0 || visible.All(item => item.IsSelected)
            ? true : visible.All(item => !item.IsSelected) ? false : null;
        SetProperty(ref _selectAll, state, nameof(SelectAll));
    }

    private async Task RunCheckedAsync()
    {
        if (!BuildValidity.CanInitialize) return;

        // 启动时固定本轮队列；配置设备期间用户再勾选其他项，不影响本轮执行。
        var list = VisibleCases.Where(x => x.IsSelected).ToArray();
        if (list.Length == 0) return;

        try
        {
            if (_outputDirectoryError is not null) throw new InvalidOperationException(_outputDirectoryError);
            ServiceLocator.Current.OutputStorage.ChangeDirectory(FailWaveformDirectory, _writer);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"日志、测试报告及FAIL波形保存目录不可用：\n{ex.Message}", "目录错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        StatusText = "正在自动配置电源、WDA、HTTP 与 Token…";
        _setupCts = new CancellationTokenSource();
        UpdateRunCommandStates();
        try
        {
            await _connect.EnsureConnectedAsync(_setupCts.Token).ConfigureAwait(true);
            await _phone.EnsureAutomationReadyAsync(_setupCts.Token).ConfigureAwait(true);
            StatusText = "正在从 PLAUD HTTP 读取样机 SN…";
            string serial;
            using (var serialClient = _phone.CreateAppClient())
            {
                try
                {
                    serial = await serialClient.ReadBoundDeviceSerialAsync(_setupCts.Token).ConfigureAwait(true);
                }
                catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
                {
                    _logger.LogWarning(ex, "PLAUD HTTP 尚未返回样机 SN；尝试打开 PLAUD 后重读");
                    StatusText = "PLAUD 尚未提供 SN，正在打开 App 后重读…";
                    await (_phone.Controller ?? throw new InvalidOperationException("WDA 尚未连接"))
                        .AppToScreenOnAsync(_setupCts.Token).ConfigureAwait(true);
                    serial = await serialClient.ReadBoundDeviceSerialAsync(_setupCts.Token).ConfigureAwait(true);
                }
            }
            DeviceSerial = serial;
            _logger.LogInformation("本轮样机 SN 已从 PLAUD HTTP 自动读取：{sn}", DeviceSerial);
            LoadPlan(list.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase));
            list = VisibleCases.Where(item => item.IsSelected).ToArray();
            _setupCts.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消自动配置，未开始采样";
            _logger.LogInformation("用户取消自动配置，未开始采样");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动配置失败：{reason}", ex.Message);
            StatusText = $"开始测试失败：{ex.Message}";
            MessageBox.Show(ex.Message, "自动配置失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            _setupCts.Dispose();
            _setupCts = null;
            UpdateRunCommandStates();
        }

        if (_connect.PowerSource is null)
            throw new InvalidOperationException("电源连接已完成但未获得电源实例");

        if (_workbook is null)
        {
            MessageBox.Show("请先加载测试计划", "未加载", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText = "环境准备完成，开始执行测试";

        _writer?.Dispose();
        _writer = null;
        _writer = WorkbookWriter.OpenAndBackup(
            ServiceLocator.Current.LoggerFactory.CreateLogger<WorkbookWriter>(),
            _workbook,
            ServiceLocator.Current.OutputStorage.CreateReportPath(SelectedSuite?.DisplayName ?? "功耗测试"),
            SelectedSuite?.ReportTemplatePath,
            SelectedSuite?.SequencePath,
            SelectedSuite?.StandardsPath,
            SelectedSuite?.StandardsColumnIndex ?? 6,
            SelectedSuite?.ReportStandardsColumnIndex ?? 7,
            SelectedSuite?.ReportResultColumnIndex ?? 8);
        _logger.LogInformation("本轮测试报告自动保存至：{path}", _writer.FilePath);
        ExportReportCommand.RaiseCanExecuteChanged();

        using var app = _phone.CreateAppClient();
        var sm = new StateMachine(_connect.PowerSource, _phone.Controller, app, DeviceSerial, _prompt,
            ServiceLocator.Current.LoggerFactory.CreateLogger<StateMachine>(),
            allowAudibleActions: true,
            // 每个勾选项按自己的动作执行，不借用未选用例的场景。
            allowFindMyBinding: true);
        _engine = new TestEngine(_connect.PowerSource, sm, _writer,
            ServiceLocator.Current.LoggerFactory.CreateLogger<TestEngine>());

        _engine.OnCaseStarted += OnEngineCaseStarted;
        _engine.OnCaseFinished += OnEngineCaseFinished;
        _engine.OnSample += s => LiveMa = s;
        _engine.OnRemainingSeconds += s => RemainingSeconds = s;

        _runCts = new CancellationTokenSource();
        UpdateRunCommandStates();
        try
        {
            foreach (var vm in list)
            {
                if (_runCts.IsCancellationRequested) break;
                while (IsPaused)
                    await Task.Delay(200, _runCts.Token).ConfigureAwait(true);
                _caseCts?.Dispose();
                _caseCts = CancellationTokenSource.CreateLinkedTokenSource(_runCts.Token);
                UpdateRunCommandStates();
                try
                {
                    StatusText = $"正在执行 {vm.Id}";
                    var result = await _engine.RunOneAsync(vm.Source, _caseCts.Token).ConfigureAwait(true);
                    if (!double.IsFinite(result.MeasuredMa))
                    {
                        StatusText = $"{vm.Id} 未完成；继续执行其他已勾选项";
                        _logger.LogWarning("{message}。当前项结果已单独记录。", StatusText);
                    }
                }
                catch (OperationCanceledException) when (!_runCts.IsCancellationRequested)
                {
                    _logger.LogInformation("用户跳过用例 {id}", vm.Id);
                }
                finally
                {
                    _caseCts.Dispose();
                    _caseCts = null;
                    UpdateRunCommandStates();
                }
            }
        }
        catch (OperationCanceledException) { /* user stopped */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "run failed");
            MessageBox.Show(ex.ToString(), "运行失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            _caseCts?.Dispose();
            _caseCts = null;
            IsPaused = false;
            RunningCase = null;
            RemainingSeconds = 0;
            UpdateRunCommandStates();
        }
    }

    private void Stop()
    {
        try { _setupCts?.Cancel(); } catch { /* ignored */ }
        try { _runCts?.Cancel(); } catch { /* ignored */ }
    }

    private void BrowseFailWaveformDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择日志、测试报告及 FAIL 波形保存目录",
            InitialDirectory = Directory.Exists(FailWaveformDirectory)
                ? FailWaveformDirectory
                : RunArtifactWriter.PreferredWaveformDirectory,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            try { FailWaveformDirectory = dialog.FolderName; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "目录切换失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    private void ExportReport()
    {
        if (_writer is null || !File.Exists(_writer.FilePath)) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出 Excel 测试报告",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            InitialDirectory = FailWaveformDirectory,
            FileName = Path.GetFileName(_writer.FilePath)
        };
        if (dialog.ShowDialog() != true) return;

        _writer.SaveCopy(dialog.FileName);
        MessageBox.Show($"Excel 报告已导出：\n{dialog.FileName}", "导出完成",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnEngineCaseStarted(TestCase tc)
    {
        var vm = Cases.FirstOrDefault(c => c.Id == tc.Id);
        if (vm is null) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            RunningCase = vm;
            vm.Status = ResultStatus.Pending;
            vm.Note = "";
        });
    }

    private void OnEngineCaseFinished(TestCase tc, RunResult result)
    {
        var vm = Cases.FirstOrDefault(c => c.Id == tc.Id);
        if (vm is null) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            vm.MeasuredMa = result.MeasuredMa;
            vm.Status = result.Status;
            vm.Note = result.Note;
            vm.ActionsFinishedAt = result.ActionsFinishedAt;
            vm.MeasurementStartedAt = result.MeasurementStartedAt;
            vm.ActualMeasurementDurationSeconds = result.ActualMeasurementDurationSeconds;
            vm.RawDataPath = result.RawDataPath;
            _results.AddOrReplace(tc, result);
        });
    }
}
