using System.Diagnostics;

namespace WizAccountant.Manager;

internal sealed class PilotProcessLauncher(string repoRoot)
{
    private readonly string _serviceExe = Path.Combine(repoRoot, "src", "WizConnector.Service", "bin", "Release", "net8.0", "WizConnector.Service.exe");
    private readonly string _trayExe = Path.Combine(repoRoot, "src", "WizConnector.Tray", "bin", "Release", "net8.0-windows", "WizConnector.Tray.exe");
    private readonly string _setupExe = Path.Combine(repoRoot, "src", "WizConnector.Setup", "bin", "Release", "net8.0-windows", "WizConnector.Setup.exe");
    private readonly string _apiProject = Path.Combine(repoRoot, "src", "WizAccountant.Api", "WizAccountant.Api.csproj");
    private readonly string _serviceProject = Path.Combine(repoRoot, "src", "WizConnector.Service", "WizConnector.Service.csproj");

    public string RepoRoot => repoRoot;

    public static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "WizAccountant.slnx")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        var dev = @"C:\Users\pj\WizAccountant";
        if (File.Exists(Path.Combine(dev, "WizAccountant.slnx")))
            return dev;

        return null;
    }

    public LaunchResult StartConnectorService(string apiBaseUrl)
    {
        if (IsRunning("WizConnector.Service"))
            return LaunchResult.AlreadyRunning("WizConnector.Service");

        try
        {
            if (File.Exists(_serviceExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"$env:Connector__ApiBaseUrl='{apiBaseUrl}'; & '{_serviceExe}'\"",
                    WorkingDirectory = Path.GetDirectoryName(_serviceExe)!,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                });
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{_serviceProject}\" -c Release",
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false
                };
                psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
                psi.Environment["Connector__ApiBaseUrl"] = apiBaseUrl;
                Process.Start(psi);
            }

            return LaunchResult.Started("WizConnector.Service");
        }
        catch (Exception ex)
        {
            return LaunchResult.Error("WizConnector.Service", ex.Message);
        }
    }

    public LaunchResult StartTray()
    {
        if (IsRunning("WizConnector.Tray"))
            return LaunchResult.AlreadyRunning("WizConnector.Tray");

        try
        {
            if (!File.Exists(_trayExe))
                return LaunchResult.Error("WizConnector.Tray", "Not built. Click “Build pilot apps” first.");

            Process.Start(new ProcessStartInfo
            {
                FileName = _trayExe,
                WorkingDirectory = Path.GetDirectoryName(_trayExe)!,
                UseShellExecute = true
            });
            return LaunchResult.Started("WizConnector.Tray");
        }
        catch (Exception ex)
        {
            return LaunchResult.Error("WizConnector.Tray", ex.Message);
        }
    }

    public LaunchResult StartLocalApi(int port = 5278)
    {
        var stopped = StopListenersOnPort(port);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{_apiProject}\" --launch-profile http",
                WorkingDirectory = repoRoot,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            });
            var note = stopped > 0
                ? $"Stopped {stopped} old listener(s) on port {port}. "
                : "";
            return LaunchResult.Started(note + "WizAccountant.Api starting (new window) — wait for listening on http://localhost:5278");
        }
        catch (Exception ex)
        {
            return LaunchResult.Error("WizAccountant.Api", ex.Message);
        }
    }

    /// <summary>Stops processes listening on the port (usually a stale dotnet run API).</summary>
    public static int StopListenersOnPort(int port)
    {
        var killed = new HashSet<int>();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(startInfo);
            if (proc is null) return 0;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(8000);

            var portToken = $":{port}";
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!line.Contains(portToken, StringComparison.Ordinal))
                    continue;
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1 || !int.TryParse(parts[^1], out var pid) || pid <= 0)
                    continue;
                killed.Add(pid);
            }

            foreach (var pid in killed)
            {
                try
                {
                    var p = Process.GetProcessById(pid);
                    p.Kill(entireProcessTree: true);
                }
                catch { /* already exited */ }
            }

            if (killed.Count > 0)
                Thread.Sleep(800);
        }
        catch { /* best effort */ }

        return killed.Count;
    }

    public static int TryParseApiPort(string apiBaseUrl, int fallback = 5278)
    {
        if (Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri) && uri.Port > 0)
            return uri.Port;
        return fallback;
    }

    public LaunchResult OpenSageSetup()
    {
        try
        {
            if (File.Exists(_setupExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _setupExe,
                    WorkingDirectory = Path.GetDirectoryName(_setupExe)!,
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{Path.Combine(repoRoot, "src", "WizConnector.Setup", "WizConnector.Setup.csproj")}\" -c Release",
                    WorkingDirectory = repoRoot,
                    UseShellExecute = true
                });
            }

            return LaunchResult.Started("WizConnector.Setup");
        }
        catch (Exception ex)
        {
            return LaunchResult.Error("WizConnector.Setup", ex.Message);
        }
    }

    public LaunchResult OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return LaunchResult.Started(url);
        }
        catch (Exception ex)
        {
            return LaunchResult.Error(url, ex.Message);
        }
    }

    public LaunchResult RunPowerShellScript(string scriptName, bool newWindow = true)
    {
        var script = Path.Combine(repoRoot, "scripts", scriptName);
        if (!File.Exists(script))
            return LaunchResult.Error(scriptName, "Script not found.");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                WorkingDirectory = repoRoot,
                UseShellExecute = newWindow
            });
            return LaunchResult.Started(scriptName);
        }
        catch (Exception ex)
        {
            return LaunchResult.Error(scriptName, ex.Message);
        }
    }

    public bool ArePilotAppsBuilt() =>
        File.Exists(_serviceExe) && File.Exists(_trayExe) && File.Exists(_setupExe);

    public LaunchResult BuildPilotApps()
    {
        var script = Path.Combine(repoRoot, "scripts", "build-pilot-apps.ps1");
        if (!File.Exists(script))
            return LaunchResult.Error("Build", "scripts/build-pilot-apps.ps1 not found.");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                WorkingDirectory = repoRoot,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            });

            if (ArePilotAppsBuilt())
                return LaunchResult.Started("Build opened in new window (apps already built — rebuild only if you changed code).");
            return LaunchResult.Started("Build opened in new window — wait for “Build complete”, then continue in WizPilot.");
        }
        catch (Exception ex)
        {
            return LaunchResult.Error("Build", ex.Message);
        }
    }

    private static bool IsRunning(string name) =>
        Process.GetProcessesByName(name).Length > 0;
}

internal readonly record struct LaunchResult(bool Ok, string Message, bool IsError = false)
{
    public static LaunchResult Started(string msg) => new(true, msg);
    public static LaunchResult AlreadyRunning(string msg) => new(true, $"{msg} is already running.");
    public static LaunchResult Error(string what, string msg) => new(false, $"{what}: {msg}", true);
}
