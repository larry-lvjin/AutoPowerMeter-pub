using AutoPowerMeter.Models;
using Microsoft.Extensions.Logging;

namespace AutoPowerMeter.Instruments;

/// <summary>
/// 无硬件时使用：电流读数 = 基线 + 随机噪声 + 缓变正弦。
/// 用于离线测试 GUI、test plan、Excel 回写流程。
/// </summary>
public sealed class MockPowerSource : IPowerSource
{
    private readonly ILogger<MockPowerSource> _logger;
    private readonly Random _rng = new();
    private double _voltage;
    private double _currentLimit = 1.0;
    private bool _output;
    private CurrentRange _range = CurrentRange.Auto;
    private double _baselineMa = 0.30; // 默认 300µA 待机

    public bool IsConnected { get; private set; }
    public string? ResourceName { get; private set; }
    public string? IdentificationString { get; private set; }

    public MockPowerSource(ILogger<MockPowerSource> logger) { _logger = logger; }

    /// <summary>测试时调用：模拟设备处于不同工作态对应的电流基线 (mA)。</summary>
    public void SetSimulatedBaseline(double baselineMa) => _baselineMa = baselineMa;

    public Task OpenAsync(string resourceName, CancellationToken ct = default)
    {
        IsConnected = true;
        ResourceName = resourceName;
        IdentificationString = "MOCK,100K12-SIM,0,1.0";
        _logger.LogInformation("[MOCK] open {res}", resourceName);
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        IsConnected = false;
        ResourceName = null;
        IdentificationString = null;
        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken ct = default)
    {
        _voltage = 0; _output = false; _currentLimit = 1.0;
        _logger.LogInformation("[MOCK] reset");
        return Task.CompletedTask;
    }

    public Task SetVoltageAsync(double volts, CancellationToken ct = default)
    {
        PowerSafety.ValidateVoltage(volts);
        _voltage = volts;
        _logger.LogInformation("[MOCK] V={v}V", volts);
        return Task.CompletedTask;
    }

    public Task SetCurrentLimitAsync(double amps, CancellationToken ct = default)
    {
        _currentLimit = amps;
        return Task.CompletedTask;
    }

    public Task SetOutputAsync(bool on, CancellationToken ct = default)
    {
        if (on) PowerSafety.ValidateVoltage(_voltage);
        _output = on;
        _logger.LogInformation("[MOCK] OUTP {state}", on ? "ON" : "OFF");
        return Task.CompletedTask;
    }

    public Task SetRangeAsync(CurrentRange range, CancellationToken ct = default)
    {
        _range = range;
        return Task.CompletedTask;
    }

    public Task<double> GetVoltageAsync(CancellationToken ct = default) => Task.FromResult(_voltage);

    public Task<double> MeasureVoltageMvAsync(CancellationToken ct = default)
        => Task.FromResult(_output ? _voltage * 1000.0 : 0.0);

    public Task<double> MeasureCurrentMaAsync(CancellationToken ct = default)
    {
        if (!_output) return Task.FromResult(0.0);
        var noise = (_rng.NextDouble() - 0.5) * 0.05 * _baselineMa;
        var slow = Math.Sin(DateTime.UtcNow.Ticks / 1e8) * 0.02 * _baselineMa;
        return Task.FromResult(Math.Max(0, _baselineMa + noise + slow));
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}

public sealed class MockPowerSourceFactory : IPowerSourceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    public MockPowerSourceFactory(ILoggerFactory loggerFactory) { _loggerFactory = loggerFactory; }

    public IReadOnlyList<string> ListResources()
        => new[] { "MOCK::1::INSTR", "MOCK::2::INSTR" };

    public IPowerSource Create() => new MockPowerSource(_loggerFactory.CreateLogger<MockPowerSource>());
}
