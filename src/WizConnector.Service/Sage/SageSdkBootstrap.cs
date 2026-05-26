using System.Reflection;
using System.Runtime.InteropServices;

namespace WizConnector.Service.Sage;

internal static class SageSdkBootstrap
{
    private static readonly string DefaultInstallPath = @"C:\Program Files (x86)\Sage Evolution";

    public static void Initialize()
    {
        var sdkPath = Environment.GetEnvironmentVariable("WIZ_SAGE_SDK_PATH");
        if (string.IsNullOrWhiteSpace(sdkPath) || !Directory.Exists(sdkPath))
            sdkPath = DefaultInstallPath;

        if (Directory.Exists(sdkPath))
        {
            NativeMethods.SetDllDirectory(sdkPath);
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!pathEnv.Contains(sdkPath, StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("PATH", sdkPath + ";" + pathEnv);
        }

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ResolveAssembly(args.Name, sdkPath);
    }

    private static Assembly? ResolveAssembly(string assemblyName, string sdkPath)
    {
        var requested = new AssemblyName(assemblyName);
        var simpleName = requested.Name;
        if (simpleName is null) return null;

        if (!simpleName.StartsWith("Pastel.", StringComparison.OrdinalIgnoreCase)
            && !simpleName.StartsWith("Evolution", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(simpleName, "System.Data.SqlClient", StringComparison.OrdinalIgnoreCase)
            && !simpleName.StartsWith("System.Configuration", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var dir in new[] { sdkPath, AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
            var file = Path.Combine(dir, simpleName + ".dll");
            if (File.Exists(file))
                return Assembly.LoadFrom(file);
        }

        return null;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetDllDirectory(string lpPathName);
    }
}
