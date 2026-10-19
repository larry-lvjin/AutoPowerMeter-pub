using System.IO;
using System.Text;

namespace AutoPowerMeter.TestRunner;

/// <summary>EMK850+ MPA：80字节文件标识，后跟1ms记录(float平均/最小/最大、int极值位置)。</summary>
public static class MpaWaveformWriter
{
    public const int HeaderBytes = 80;
    public const int RecordBytes = 20;

    public static void Write(string path, IReadOnlyList<double> rawMa, double rate)
    {
        var perMs = checked((int)Math.Round(rate / 1000.0));
        if (perMs < 1 || Math.Abs(rate - perMs * 1000.0) > 0.001)
            throw new NotSupportedException("EMK 1ms波形要求采样率为1000Hz整数倍，不能伪造时间分辨率");
        var header = Encoding.UTF8.GetBytes(Path.GetFileName(path) + ";TM");
        if (header.Length >= HeaderBytes) throw new ArgumentException("MPA文件标识超过79字节");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(header);
        writer.Write(new byte[HeaderBytes - header.Length]);
        for (var start = 0; start < rawMa.Count; start += perMs)
        {
            var end = Math.Min(rawMa.Count, start + perMs);
            double sum = 0, min = double.PositiveInfinity, max = double.NegativeInfinity;
            var minIndex = 1; var maxIndex = 1;
            for (var i = start; i < end; i++)
            {
                var value = rawMa[i];
                if (!double.IsFinite(value) || Math.Abs(value) > float.MaxValue)
                    throw new InvalidOperationException("MPA中出现不可表示的电流值");
                sum += value;
                if (value < min) { min = value; minIndex = i - start + 1; }
                if (value > max) { max = value; maxIndex = i - start + 1; }
            }
            writer.Write((float)(sum / (end - start)));
            writer.Write((float)min); writer.Write((float)max);
            writer.Write(minIndex); writer.Write(maxIndex);
        }
    }
}
