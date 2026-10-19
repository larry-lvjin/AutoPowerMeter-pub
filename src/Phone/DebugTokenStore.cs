using System.Runtime.InteropServices;
using System.Text;

namespace AutoPowerMeter.Phone;

/// <summary>Windows凭据管理器按当前用户保护Token；不写入程序配置、命令行或日志。</summary>
public static class DebugTokenStore
{
    private const string Target = "AutoPowerMeter/PLAUD-HTTP-Debug";
    public static string Resolve() => new[]
    {
        Environment.GetEnvironmentVariable("PLAUD_HTTP_DEBUG_TOKEN"),
        Environment.GetEnvironmentVariable("PLAUD_HTTP_DEBUG_TOKEN", EnvironmentVariableTarget.User),
        Environment.GetEnvironmentVariable("PLAUD_HTTP_DEBUG_TOKEN", EnvironmentVariableTarget.Machine),
        Load()
    }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";

    public static string Load()
    {
        if (!CredRead(Target, 1, 0, out var pointer)) return "";
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            var bytes = new byte[credential.BlobSize];
            Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally { CredFree(pointer); }
    }

    public static bool Save(string token)
    {
        var bytes = Encoding.Unicode.GetBytes(token);
        var memory = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            var credential = new Credential { Type = 1, TargetName = Target, UserName = "PLAUD",
                Persist = 2, BlobSize = (uint)bytes.Length, Blob = memory };
            return CredWrite(ref credential, 0);
        }
        finally
        {
            for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(memory, i, 0);
            Marshal.FreeHGlobal(memory);
            Array.Clear(bytes);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags, Type;
        public string? TargetName, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}
