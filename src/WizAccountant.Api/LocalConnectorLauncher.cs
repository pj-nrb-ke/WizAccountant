using System.Diagnostics;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

public sealed class LocalConnectorLauncher(IConfiguration config, IHostEnvironment env, ILogger<LocalConnectorLauncher> logger)
{
    public StartLocalProgramsResponse Start()
    {
        var response = new StartLocalProgramsResponse();
        var root = ResolveRepoRoot();
        if (root is null)
        {
            response.Ok = false;
            response.Message = "Could not find the WizAccountant project folder on this PC.";
            return response;
        }

        var apiBase = config["Connector:ApiBaseUrl"] ?? "http://localhost:5278";
        var serviceExe = Path.Combine(root, "src", "WizConnector.Service", "bin", "Release", "net8.0", "WizConnector.Service.exe");
        var trayExe = Path.Combine(root, "src", "WizConnector.Tray", "bin", "Release", "net8.0-windows", "WizConnector.Tray.exe");

        TryStartProcess("WizConnector.Service", serviceExe, response, () =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(root, "src", "WizConnector.Service")}\" -c Release",
                WorkingDirectory = root,
                UseShellExecute = false
            };
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.Environment["Connector__ApiBaseUrl"] = apiBase;
            return psi;
        });

        TryStartProcess("WizConnector.Tray", trayExe, response, () => new ProcessStartInfo
        {
            FileName = trayExe,
            WorkingDirectory = Path.GetDirectoryName(trayExe)!,
            UseShellExecute = true
        });

        response.Ok = response.Errors.Count == 0;
        response.Message = response.Ok
            ? "Connector programs are starting. Wait a few seconds, then refresh Sites and run Test connection."
            : string.Join(" ", response.Errors);
        return response;
    }

    public StartLocalProgramsResponse OpenSageSetup()
    {
        var response = new StartLocalProgramsResponse();
        var root = ResolveRepoRoot();
        if (root is null)
        {
            response.Ok = false;
            response.Message = "Could not find the WizAccountant project folder on this PC.";
            return response;
        }

        var setupExe = Path.Combine(root, "src", "WizConnector.Setup", "bin", "Release", "net8.0-windows", "WizConnector.Setup.exe");
        TryStartProcess("WizConnector.Setup", setupExe, response, () => new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(root, "src", "WizConnector.Setup")}\" -c Release",
            WorkingDirectory = root,
            UseShellExecute = true
        });

        response.Ok = response.Errors.Count == 0;
        response.Message = response.Ok ? "Sage setup window opened." : string.Join(" ", response.Errors);
        return response;
    }

    private void TryStartProcess(string name, string exePath, StartLocalProgramsResponse response, Func<ProcessStartInfo> fallback)
    {
        if (IsRunning(name))
        {
            response.AlreadyRunning.Add(name);
            return;
        }

        try
        {
            ProcessStartInfo psi;
            if (File.Exists(exePath))
            {
                if (name == "WizConnector.Service")
                {
                    var apiBase = config["Connector:ApiBaseUrl"] ?? "http://localhost:5278";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoExit -Command \"$env:Connector__ApiBaseUrl='{apiBase}'; & '{exePath}'\"",
                        WorkingDirectory = Path.GetDirectoryName(exePath)!,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    });
                    response.Started.Add(name);
                    return;
                }

                psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath)!,
                    UseShellExecute = true
                };
            }
            else
            {
                logger.LogWarning("{Name} not built at {Path}; using dotnet run.", name, exePath);
                psi = fallback();
            }

            Process.Start(psi);
            response.Started.Add(name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start {Name}", name);
            response.Errors.Add($"{name}: {ex.Message}");
        }
    }

    private static bool IsRunning(string processName) =>
        Process.GetProcessesByName(processName).Length > 0;

    private string? ResolveRepoRoot()
    {
        var configured = config["WizAccountant:RepoRoot"];
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "WizAccountant.slnx")))
            return Path.GetFullPath(configured);

        foreach (var start in new[] { env.ContentRootPath, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "WizAccountant.slnx")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }
        }

        return null;
    }
}
