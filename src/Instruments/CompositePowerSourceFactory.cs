using Microsoft.Extensions.Logging;

namespace AutoPowerMeter.Instruments;

/// <summary>
/// 合并 100K12++ 串口、历史 VISA 与 Mock 资源。
/// </summary>
public sealed class CompositePowerSourceFactory : IPowerSourceFactory
{
    private readonly Agilent66319DFactory _real;
    private readonly Power100K12Factory _serial;
    private readonly MockPowerSourceFactory _mock;
    private readonly ILoggerFactory _lf;

    public CompositePowerSourceFactory(ILoggerFactory loggerFactory)
    {
        _real = new Agilent66319DFactory(loggerFactory);
        _serial = new Power100K12Factory(loggerFactory);
        _mock = new MockPowerSourceFactory(loggerFactory);
        _lf = loggerFactory;
    }

    public IReadOnlyList<string> ListResources()
    {
        var combined = new List<string>();
        combined.AddRange(_serial.ListResources());
        // VISA implementations can block for a long time while their native resource
        // manager starts.  Resource discovery runs on the WPF UI thread, so bound the
        // legacy scan instead of preventing the serial power supply UI from opening.
        var visaScan = Task.Run(_real.ListResources);
        if (visaScan.Wait(TimeSpan.FromSeconds(1)))
            combined.AddRange(visaScan.Result);
        else
            _lf.CreateLogger<CompositePowerSourceFactory>()
                .LogWarning("VISA resource scan timed out after 1 second; serial resources remain available");
        combined.AddRange(_mock.ListResources());
        return combined;
    }

    public IPowerSource Create() => throw new NotSupportedException("Use CreateForResource");

    public IPowerSource CreateForResource(string resourceName)
        => resourceName.StartsWith("MOCK::", StringComparison.OrdinalIgnoreCase)
            ? _mock.Create()
            : resourceName.StartsWith("SERIAL::", StringComparison.OrdinalIgnoreCase)
                ? _serial.Create()
                : _real.Create();
}
