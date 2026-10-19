using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AutoPowerMeter.Utils;

namespace AutoPowerMeter;

/// <summary>
/// 把日志缓冲到内存里，给 GUI 底部日志面板订阅。
/// </summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    public static InMemoryLoggerProvider Instance { get; private set; } = null!;

    private readonly ConcurrentDictionary<string, InMemoryLogger> _loggers = new();
    public event Action<LogEntry>? OnLog;
    private readonly RunOutputStorage? _storage;

    public InMemoryLoggerProvider(RunOutputStorage? storage = null)
    {
        Instance = this;
        _storage = storage;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, n => new InMemoryLogger(n, this));

    public void Dispose() { }

    internal void Emit(LogEntry entry)
    {
        // 在UI调度前落盘，包含Debug和异常，关闭窗口不丢尚未刷到界面的记录。
        _storage?.Append(entry);
        if (entry.Level >= LogLevel.Information) OnLog?.Invoke(entry);
    }
}

public sealed record LogEntry(DateTime Time, LogLevel Level, string Category, string Message, Exception? Exception);

internal sealed class InMemoryLogger : ILogger
{
    private readonly string _category;
    private readonly InMemoryLoggerProvider _owner;

    public InMemoryLogger(string category, InMemoryLoggerProvider owner)
    {
        _category = category;
        _owner = owner;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var msg = formatter(state, exception);
        _owner.Emit(new LogEntry(DateTime.Now, logLevel, _category, msg, exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
