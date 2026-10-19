namespace AutoPowerMeter.Instruments;

/// <summary>独立于消费队列的连续环形缓存；UI读取/管线排空不会删除这里的前置波形。</summary>
public sealed class CurrentHistory(int capacity)
{
    private readonly double[] _values = new double[capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    private readonly object _gate = new();
    private long _next, _first;
    public long NextSequence { get { lock (_gate) return _next; } }
    public long Add(double value)
    {
        lock (_gate)
        {
            var index = _next++;
            _values[index % _values.Length] = value;
            _first = Math.Max(_first, _next - _values.Length);
            return index;
        }
    }
    public void Clear() { lock (_gate) _first = _next; }
    public (long First, double[] Values) Read(long first, long endExclusive)
    {
        lock (_gate)
        {
            first = Math.Max(first, _first);
            var end = Math.Min(endExclusive, _next);
            var values = new double[Math.Max(0, end - first)];
            for (var i = 0; i < values.Length; i++) values[i] = _values[(first + i) % _values.Length];
            return (first, values);
        }
    }
}

public sealed record ContextWaveform(IReadOnlyList<double> Samples, int BeforeSamples,
    int MeasurementSamples, int AfterSamples, string Note)
{
    /// <summary>第一笔波形样本到达主机的时间；未提供时由测量窗口反推。</summary>
    public DateTime? StartedAt { get; init; }
}

public interface IContextWaveformSource
{
    /// <summary>
    /// 结束当前用例的临时电流缓存并返回该用例从开始到判定完成的全部波形。
    /// 此方法只负责保存已经产生的仪表数据，不会为了波形再延长用例。
    /// </summary>
    Task<ContextWaveform> CompleteFailWaveformAsync(SampleResult measurement,
        CancellationToken ct = default);
}

/// <summary>
/// 从一条自动用例开始就连续缓存仪表原始流。实现应先写临时文件，Pass/跳过时删除，只有 Fail 才保存。
/// </summary>
public interface ICaseWaveformSource : IContextWaveformSource
{
    void BeginCaseWaveform(DateTime caseStartedAt);
    void DiscardCaseWaveform();
}
