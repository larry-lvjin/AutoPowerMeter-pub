using System.IO;
using AutoPowerMeter.Instruments;
using AutoPowerMeter.Phone;
using Microsoft.Extensions.Logging;
using AutoPowerMeter.Utils;

namespace AutoPowerMeter;

/// <summary>
/// 轻量服务定位器。WPF + 单窗口场景下足够；避免引入 DI 框架。
/// </summary>
public sealed class ServiceLocator
{
    public static ServiceLocator Current { get; private set; } = null!;

    public ILoggerFactory LoggerFactory { get; }
    public CompositePowerSourceFactory PowerSourceFactory { get; }
    public InMemoryLoggerProvider LogSink { get; }
    public string TapMapPath { get; }
    public RunOutputStorage OutputStorage { get; }
    public IosRuntimeBootstrapper IosRuntime { get; }

    private ServiceLocator(ILoggerFactory loggerFactory, CompositePowerSourceFactory psFactory,
        InMemoryLoggerProvider sink, string tapMapPath, RunOutputStorage outputStorage)
    {
        LoggerFactory = loggerFactory;
        PowerSourceFactory = psFactory;
        LogSink = sink;
        TapMapPath = tapMapPath;
        OutputStorage = outputStorage;
        IosRuntime = new IosRuntimeBootstrapper(new IosRuntimeHost(loggerFactory.CreateLogger<IosRuntimeHost>()),
            loggerFactory.CreateLogger<IosRuntimeBootstrapper>());
    }

    public static void Initialize()
    {
        var outputStorage = new RunOutputStorage(Path.Combine(RuntimeSettingsStorage.DirectoryPath, "output-directory.json"));
        var sink = new InMemoryLoggerProvider(outputStorage);
        var lf = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddDebug()
                .AddProvider(sink);
        });
        var ps = new CompositePowerSourceFactory(lf);
        var tapMapPath = RuntimeSettingsStorage.ResolveTapMap(Path.Combine(AppContext.BaseDirectory, "Resources", "tap_map.json"));
        Current = new ServiceLocator(lf, ps, sink, tapMapPath, outputStorage);
    }

    /// <summary>新建一个 WdaPhoneController（连接逻辑由 PhoneViewModel 触发）。</summary>
    public IPhoneController CreateWdaPhone() => new WdaPhoneController(TapMapPath, LoggerFactory, IosRuntime);

}
