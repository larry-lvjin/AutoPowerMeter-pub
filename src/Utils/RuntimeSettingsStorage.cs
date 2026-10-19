using System.IO;

namespace AutoPowerMeter.Utils;

/// <summary>Keep editable settings outside the single-file extraction cache.</summary>
internal static class RuntimeSettingsStorage
{
    internal static string DirectoryPath
    {
        get
        {
            var executableDirectory = Path.GetDirectoryName(Environment.ProcessPath);
            return string.Equals(executableDirectory?.TrimEnd(Path.DirectorySeparatorChar),
                AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(AppContext.BaseDirectory, "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutoPowerMeter", "settings");
        }
    }

    internal static string ResolveTapMap(string bundledPath)
    {
        var executableDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.Equals(executableDirectory?.TrimEnd(Path.DirectorySeparatorChar),
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            return bundledPath;

        var directory = DirectoryPath;
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "tap_map.json");
        if (!File.Exists(target)) File.Copy(bundledPath, target, overwrite: false);
        return target;
    }
}
