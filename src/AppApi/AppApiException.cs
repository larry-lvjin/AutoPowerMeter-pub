namespace AutoPowerMeter.AppApi;

public sealed class AppApiException : Exception
{
    public int Code { get; }
    public bool IsRetryable => Code is 1007 or 1008;

    public AppApiException(int code, string reason)
        : base($"APP HTTP {code}: {reason}")
    {
        Code = code;
    }
}
