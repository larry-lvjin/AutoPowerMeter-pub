using System;
using System.Globalization;

namespace AutoPowerMeter.Utils;

/// <summary>
/// 电流单位自适应显示：值 &lt; 1 mA 用 µA，否则用 mA。
/// 例：0.03 mA → "30 µA"；1.42 mA → "1.42 mA"；78.76 mA → "78.8 mA"。
/// </summary>
public static class UnitFormatter
{
    private const double MaThreshold = 1.0;
    private const string Micro = "µA";
    private const string Milli = "mA";

    /// <summary>用 mA 输入，返回带单位字符串。</summary>
    public static string FormatCurrentMa(double valueMa, int sigDigits = 3)
    {
        if (double.IsNaN(valueMa)) return "—";
        var abs = Math.Abs(valueMa);
        if (abs < MaThreshold)
        {
            var ua = valueMa * 1000.0;
            return $"{FormatSig(ua, sigDigits)} {Micro}";
        }
        return $"{FormatSig(valueMa, sigDigits)} {Milli}";
    }

    /// <summary>把 µA 输入归一化为 mA 内部值。</summary>
    public static double UaToMa(double valueUa) => valueUa / 1000.0;

    /// <summary>把 mA 输入转 µA。</summary>
    public static double MaToUa(double valueMa) => valueMa * 1000.0;

    /// <summary>
    /// 把测得 mA 值按 limit 单位偏好回写到 Excel：uA 四舍五入取整，mA 四舍五入保留两位。
    /// 使用 AwayFromZero，确保正负半值都符合常规“四舍五入”预期。
    /// </summary>
    public static double NormalizeForExcel(double valueMa, bool limitInMicroAmps)
        => limitInMicroAmps
            ? Math.Round(MaToUa(valueMa), 0, MidpointRounding.AwayFromZero)
            : Math.Round(valueMa, 2, MidpointRounding.AwayFromZero);

    private static string FormatSig(double value, int sig)
    {
        if (value == 0) return "0";
        var abs = Math.Abs(value);
        var digitsBeforeDecimal = (int)Math.Floor(Math.Log10(abs)) + 1;
        var decimals = Math.Max(0, sig - digitsBeforeDecimal);
        return value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }
}
