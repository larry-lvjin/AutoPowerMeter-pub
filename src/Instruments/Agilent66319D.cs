using System.Globalization;
using AutoPowerMeter.Models;
using Ivi.Visa;
using Microsoft.Extensions.Logging;

namespace AutoPowerMeter.Instruments;

/// <summary>
/// Keysight (Agilent) 66319D Mobile Communications DC Source — GPIB/VISA 驱动。
///
/// 66319D 通道：Output 1 = Main (DUT 供电)，Output 2 = Aux (keep-alive)。本驱动只用 Output 1。
/// 量程：高量程 ~3A，低量程 ~20mA（约 1µA 分辨率）。
/// </summary>
public sealed class Agilent66319D : IPowerSource
{
    private readonly ILogger<Agilent66319D> _logger;
    private readonly SemaphoreSlim _io = new(1, 1);
    private IMessageBasedSession? _session;

    public bool IsConnected => _session is not null;
    public string? ResourceName { get; private set; }
    public string? IdentificationString { get; private set; }

    public Agilent66319D(ILogger<Agilent66319D> logger)
    {
        _logger = logger;
    }

    public async Task OpenAsync(string resourceName, CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_session is not null) await CloseInternalAsync().ConfigureAwait(false);
            _logger.LogInformation("opening VISA resource {res}", resourceName);
            var raw = GlobalResourceManager.Open(resourceName) as IMessageBasedSession
                ?? throw new InvalidOperationException("Opened resource is not message-based");
            raw.TimeoutMilliseconds = 5000;
            _session = raw;
            ResourceName = resourceName;
            IdentificationString = await QueryNoLockAsync("*IDN?", ct).ConfigureAwait(false);
            _logger.LogInformation("connected: {idn}", IdentificationString);

            // 基础初始化
            await WriteNoLockAsync("*CLS", ct).ConfigureAwait(false);
            await WriteNoLockAsync("INST:NSEL 1", ct).ConfigureAwait(false);  // 主输出
        }
        catch
        {
            await CloseInternalAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task CloseAsync()
    {
        await _io.WaitAsync().ConfigureAwait(false);
        try { await CloseInternalAsync().ConfigureAwait(false); }
        finally { _io.Release(); }
    }

    private Task CloseInternalAsync()
    {
        try { _session?.Dispose(); }
        catch (Exception ex) { _logger.LogWarning(ex, "error closing VISA session"); }
        _session = null;
        ResourceName = null;
        IdentificationString = null;
        return Task.CompletedTask;
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            await WriteNoLockAsync("*RST", ct).ConfigureAwait(false);
            await WriteNoLockAsync("*CLS", ct).ConfigureAwait(false);
            await WriteNoLockAsync("INST:NSEL 1", ct).ConfigureAwait(false);
        }
        finally { _io.Release(); }
    }

    public async Task SetVoltageAsync(double volts, CancellationToken ct = default)
    {
        PowerSafety.ValidateVoltage(volts);
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            await WriteNoLockAsync($"SOUR:VOLT {volts.ToString("0.####", CultureInfo.InvariantCulture)}", ct).ConfigureAwait(false);
        }
        finally { _io.Release(); }
    }

    public async Task SetCurrentLimitAsync(double amps, CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            await WriteNoLockAsync($"SOUR:CURR {amps.ToString("0.####", CultureInfo.InvariantCulture)}", ct).ConfigureAwait(false);
        }
        finally { _io.Release(); }
    }

    public async Task SetOutputAsync(bool on, CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            if (on)
            {
                var configured = double.Parse(await QueryNoLockAsync("SOUR:VOLT?", ct).ConfigureAwait(false),
                    CultureInfo.InvariantCulture);
                PowerSafety.ValidateVoltage(configured);
            }
            await WriteNoLockAsync(on ? "OUTP ON" : "OUTP OFF", ct).ConfigureAwait(false);
        }
        finally { _io.Release(); }
    }

    public async Task SetRangeAsync(CurrentRange range, CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            switch (range)
            {
                case CurrentRange.Low:
                    await WriteNoLockAsync("SENS:CURR:RANG MIN", ct).ConfigureAwait(false);
                    break;
                case CurrentRange.High:
                    await WriteNoLockAsync("SENS:CURR:RANG MAX", ct).ConfigureAwait(false);
                    break;
                case CurrentRange.Auto:
                default:
                    // 66319D 无真自动量程；策略：默认设高量程，调用方按用例切到 Low
                    await WriteNoLockAsync("SENS:CURR:RANG MAX", ct).ConfigureAwait(false);
                    break;
            }
        }
        finally { _io.Release(); }
    }

    public async Task<double> GetVoltageAsync(CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            var s = await QueryNoLockAsync("SOUR:VOLT?", ct).ConfigureAwait(false);
            return ParseDouble(s);
        }
        finally { _io.Release(); }
    }

    public async Task<double> MeasureVoltageMvAsync(CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            var s = await QueryNoLockAsync("MEAS:VOLT?", ct).ConfigureAwait(false);
            return ParseDouble(s) * 1000.0; // V → mV
        }
        finally { _io.Release(); }
    }

    public async Task<double> MeasureCurrentMaAsync(CancellationToken ct = default)
    {
        await _io.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            var s = await QueryNoLockAsync("MEAS:CURR?", ct).ConfigureAwait(false);
            return ParseDouble(s) * 1000.0; // A → mA
        }
        finally { _io.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        try { _session?.Dispose(); } catch { /* ignored */ }
        _session = null;
        return ValueTask.CompletedTask;
    }

    // ---------------- internals ----------------

    private void EnsureOpen()
    {
        if (_session is null)
            throw new InvalidOperationException("Power source is not connected");
    }

    private Task WriteNoLockAsync(string scpi, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            _logger.LogTrace("→ {scpi}", scpi);
            _session!.FormattedIO.WriteLine(scpi);
        }, ct);
    }

    private Task<string> QueryNoLockAsync(string scpi, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            _logger.LogTrace("⇆ {scpi}", scpi);
            _session!.FormattedIO.WriteLine(scpi);
            var reply = _session.FormattedIO.ReadLine().Trim();
            _logger.LogTrace("← {reply}", reply);
            return reply;
        }, ct);
    }

    private static double ParseDouble(string s)
    {
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        // 兼容仪器偶尔回 "+1.234E-03,..." 多字段
        var first = s.Split(',', ';')[0];
        return double.Parse(first, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}

public sealed class Agilent66319DFactory : IPowerSourceFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public Agilent66319DFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IReadOnlyList<string> ListResources()
    {
        try
        {
            return GlobalResourceManager.Find("?*INSTR").ToList();
        }
        catch (Exception)
        {
            // VISA 运行时未装时不要让整个程序崩
            return Array.Empty<string>();
        }
    }

    public IPowerSource Create() => new Agilent66319D(_loggerFactory.CreateLogger<Agilent66319D>());
}
