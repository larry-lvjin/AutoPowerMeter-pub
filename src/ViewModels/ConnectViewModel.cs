using System.Collections.ObjectModel;
using AutoPowerMeter.Instruments;
using AutoPowerMeter.Models;
using Microsoft.Extensions.Logging;

namespace AutoPowerMeter.ViewModels;

/// <summary>
/// 测试电源运行时连接状态。UI 不再暴露手工配置；开始测试时自动发现并连接。
/// </summary>
public sealed class ConnectViewModel : ObservableObject
{
    private readonly CompositePowerSourceFactory _powerSourceFactory;
    private readonly ILogger<ConnectViewModel> _logger;
    private IPowerSource? _powerSource;

    public ConnectViewModel()
    {
        _powerSourceFactory = ServiceLocator.Current.PowerSourceFactory;
        _logger = ServiceLocator.Current.LoggerFactory.CreateLogger<ConnectViewModel>();
        RefreshResources();
    }

    public ObservableCollection<string> AvailableResources { get; } = new();

    private string? _selectedResource;
    public string? SelectedResource
    {
        get => _selectedResource;
        private set => SetProperty(ref _selectedResource, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    private string _statusText = "等待开始测试";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public double VoltageInput { get; } = PowerSafety.DefaultTestVoltageV;
    public double CurrentLimitInput { get; } = 1.0;
    public IPowerSource? PowerSource => _powerSource;

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (!AutoPowerMeter.Utils.BuildValidity.CanInitialize) return;

        if (_powerSource is not null)
        {
            try
            {
                if (_powerSource.IsConnected)
                {
                    await _powerSource.SetVoltageAsync(VoltageInput, cancellationToken).ConfigureAwait(true);
                    IsConnected = true;
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogWarning(ex, "旧电源连接已失效，自动重新枚举并连接"); }
            await _powerSource.DisposeAsync().ConfigureAwait(true);
            _powerSource = null;
            IsConnected = false;
        }
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try { await ConnectOnceAsync(cancellationToken).ConfigureAwait(true); return; }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < 3)
            {
                _logger.LogWarning(ex, "电源自动连接第{attempt}/3次失败，重新扫描", attempt);
                await Task.Delay(1000, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    private async Task ConnectOnceAsync(CancellationToken cancellationToken)
    {

        RefreshResources();
        var candidates = AvailableResources
            .Where(resource => !resource.StartsWith("MOCK::", StringComparison.OrdinalIgnoreCase))
            .OrderBy(resource => resource.Contains("::COM12::", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(resource => resource, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("未发现测试电源串口/VISA 资源");

        var failures = new List<string>();
        foreach (var resource in candidates)
        {
            IPowerSource? candidate = null;
            try
            {
                StatusText = $"正在探测 {resource}";
                candidate = _powerSourceFactory.CreateForResource(resource);
                await candidate.OpenAsync(resource, cancellationToken).ConfigureAwait(true);

                PowerSafety.ValidateVoltage(VoltageInput);
                await candidate.SetCurrentLimitAsync(CurrentLimitInput, cancellationToken).ConfigureAwait(true);
                await candidate.SetVoltageAsync(VoltageInput, cancellationToken).ConfigureAwait(true);
                var configuredVoltage = await candidate.GetVoltageAsync(cancellationToken).ConfigureAwait(true);
                if (!double.IsFinite(configuredVoltage) || configuredVoltage > PowerSafety.MaximumDeviceVoltageV)
                    throw new InvalidOperationException($"电源配置值 {configuredVoltage:0.###} V 不满足 4.0 V 硬上限");

                _powerSource = candidate;
                SelectedResource = resource;
                IsConnected = true;
                StatusText = candidate is Power100K12
                    ? $"已连接 {resource}，{VoltageInput:0.0} V 设置指令已确认（仪器不支持软件限流）"
                    : $"已连接 {resource}，{VoltageInput:0.0} V / {CurrentLimitInput:0.0} A";
                _logger.LogInformation("测试开始前已自动识别并连接电源 {resource}", resource);
                return;
            }
            catch (OperationCanceledException)
            {
                if (candidate is not null) await candidate.DisposeAsync().ConfigureAwait(true);
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add($"{resource}: {ex.Message}");
                _logger.LogWarning(ex, "候选资源 {resource} 不是可用测试电源，继续探测", resource);
                if (candidate is not null)
                {
                    try { await candidate.DisposeAsync().ConfigureAwait(true); } catch { }
                }
            }
        }

        _powerSource = null;
        IsConnected = false;
        StatusText = "自动连接失败";
        throw new InvalidOperationException("未找到可响应 4.0 V 设置指令的测试电源：" +
                                            string.Join("；", failures));
    }

    private void RefreshResources()
    {
        AvailableResources.Clear();
        foreach (var resource in _powerSourceFactory.ListResources())
            AvailableResources.Add(resource);
        SelectedResource = AvailableResources
                               .FirstOrDefault(resource => resource.Contains("::COM12::", StringComparison.OrdinalIgnoreCase))
                           ?? AvailableResources.FirstOrDefault(resource =>
                               !resource.StartsWith("MOCK::", StringComparison.OrdinalIgnoreCase));
    }
}
