namespace AutoPowerMeter.Models;

public sealed record CurrentBlock(double ElapsedSeconds, double MeanMa);
public sealed record StabilityEvidence(DateTime MonitoringStartedAt, DateTime ConfirmedAt,
    double WaitSeconds, int WindowSeconds, double MeanMa, double BlockStdDevMa,
    double BlockRangeMa, double AllowedRangeMa, IReadOnlyList<CurrentBlock> OneSecondMeans);

public sealed record CaptureQuality(int RawSampleCount, double SampleCoverageSeconds,
    double ReceiveDurationSeconds, double MeanMa, double StdDevMa, double MinMa, double MaxMa,
    int ExcludedSampleCount, IReadOnlyList<CurrentBlock> FiveSecondMeans,
    IReadOnlyList<string> ReviewNotes)
{
    public static CaptureQuality Analyze(IReadOnlyList<double> samples, double rate, TimeSpan elapsed)
    {
        if (samples.Count == 0 || !double.IsFinite(rate) || rate <= 0)
            throw new InvalidOperationException("没有有效样本或采样率");
        double mean = 0, m2 = 0, min = double.PositiveInfinity, max = double.NegativeInfinity;
        var blockSize = Math.Max(1, checked((int)Math.Round(5 * rate)));
        var blocks = new List<CurrentBlock>();
        double blockSum = 0;
        var blockCount = 0;
        for (var i = 0; i < samples.Count; i++)
        {
            var value = samples[i];
            if (!double.IsFinite(value)) throw new InvalidOperationException("样本包含非有限值，禁止静默剔除");
            var delta = value - mean;
            mean += delta / (i + 1);
            m2 += delta * (value - mean);
            min = Math.Min(min, value); max = Math.Max(max, value);
            blockSum += value;
            if (++blockCount == blockSize)
            {
                blocks.Add(new CurrentBlock((i + 1) / rate, blockSum / blockCount));
                blockSum = 0; blockCount = 0;
            }
        }
        if (blockCount > 0) blocks.Add(new CurrentBlock(samples.Count / rate, blockSum / blockCount));
        var notes = new List<string>();
        if (Math.Abs(mean) > 1e-9 && max > 10 * Math.Abs(mean))
            notes.Add("存在高幅脉冲（峰值超过均值10倍），需核对业务事件；脉冲保留，不据此改变Pass/Fail");
        var fullBlocks = blocks.Take(samples.Count / blockSize).ToArray();
        if (fullBlocks.Length >= 2
            && fullBlocks.Max(b => b.MeanMa) - fullBlocks.Min(b => b.MeanMa) > Math.Max(0.050, Math.Abs(mean) * 0.25))
            notes.Add("5秒区段均值变化较大，可能存在突发活动或状态跳变；请按时间段复核，不自动裁剪或挑低值重测");
        return new(samples.Count, samples.Count / rate, elapsed.TotalSeconds, mean,
            Math.Sqrt(m2 / samples.Count), min, max, 0, blocks, notes);
    }
}
