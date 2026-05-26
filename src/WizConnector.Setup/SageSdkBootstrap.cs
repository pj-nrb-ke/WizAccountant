using System.Reflection;
using System.Runtime.InteropServices;

namespace WizConnector.Setup;

file static class NativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool SetDllDirectory(string lpPathName);
}

/// <summary>
/// Ensures Pastel.Evolution.* assemblies load from the app folder or Sage Evolution install directory.
/// </summary>
internal static class SageSdkBootstrap
{
    private static readonly string DefaultInstallPath = @"C:\Program Files (x86)\Sage Evolution";
    private static readonly string[] AssemblyPrefixes =
    [
        "Pastel.",
        "Evolution",
        "System.Data.SqlClient",
        "System.Configuration"
    ];

    public static string ResolveSdkPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("WIZ_SAGE_SDK_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return fromEnv;

        if (Directory.Exists(DefaultInstallPath))
            return DefaultInstallPath;

        return AppContext.BaseDirectory;
    }

    public static void Initialize()
    {
        var sdkPath = ResolveSdkPath();

        NativeMethods.SetDllDirectory(sdkPath);

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!pathEnv.Contains(sdkPath, StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", sdkPath + ";" + pathEnv);

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ResolveAssembly(args.Name, sdkPath);
    }

    private static Assembly? ResolveAssembly(string assemblyName, string sdkPath)
    {
        var requested = new AssemblyName(assemblyName);
        var simpleName = requested.Name;
        if (simpleName is null) return null;

        if (!AssemblyPrefixes.Any(p => simpleName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return null;

        // Prefer registered SDK in Sage Evolution install folder (not copied build output).
        foreach (var dir in new[] { sdkPath, AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

            var file = Path.Combine(dir, simpleName + ".dll");
            if (File.Exists(file))
                return Assembly.LoadFrom(file);
        }

        return null;
    }
}
