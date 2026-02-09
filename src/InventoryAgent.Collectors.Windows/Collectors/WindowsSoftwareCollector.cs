using System.Diagnostics;
using System.Management;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace InventoryAgent.Collectors.Windows;

public class WindowsSoftwareCollector : ICollector
{
    private readonly ILogger<WindowsSoftwareCollector> _logger;

    public string Name => "WindowsSoftwareCollector";

    public WindowsSoftwareCollector(ILogger<WindowsSoftwareCollector> logger)
    {
        _logger = logger;
    }

    public async Task<(object? Data, IReadOnlyList<CollectionWarning> Warnings)> CollectAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<CollectionWarning>();

        var os = await Task.Run(() => CollectOsInfo(warnings), cancellationToken);
        var installedApps = await Task.Run(() => CollectInstalledApps(warnings), cancellationToken);
        var runningProcesses = CollectRunningProcesses(warnings);

        var software = new SoftwareInfo(os, installedApps, runningProcesses);

        return (software, warnings);
    }

    private OsInfo? CollectOsInfo(List<CollectionWarning> warnings)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            var os = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (os == null)
            {
                warnings.Add(new CollectionWarning(Name, "OS", "No OS found via WMI", null));
                return null;
            }

            var name = os["Caption"]?.ToString();
            var version = os["Version"]?.ToString();
            var build = os["BuildNumber"]?.ToString();
            var architecture = os["OSArchitecture"]?.ToString();

            DateTime? bootTime = null;
            if (os["LastBootUpTime"] != null)
            {
                var bootTimeStr = os["LastBootUpTime"].ToString();
                bootTime = ManagementDateTimeConverter.ToDateTime(bootTimeStr!);
            }

            TimeSpan? uptime = null;
            if (bootTime.HasValue)
            {
                uptime = DateTime.UtcNow - bootTime.Value.ToUniversalTime();
            }

            return new OsInfo(name, version, build, null, architecture, bootTime, uptime);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "OS", "Failed to query OS info", ex.Message));
            return null;
        }
    }

    private IReadOnlyList<InstalledApp>? CollectInstalledApps(List<CollectionWarning> warnings)
    {
        try
        {
            var apps = new List<InstalledApp>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Product");
            foreach (ManagementObject app in searcher.Get())
            {
                var name = app["Name"]?.ToString();
                var version = app["Version"]?.ToString();
                var publisher = app["Vendor"]?.ToString();
                
                DateTime? installDate = null;
                if (app["InstallDate"] != null)
                {
                    var installDateStr = app["InstallDate"].ToString();
                    if (DateTime.TryParseExact(installDateStr, "yyyyMMdd", null, 
                        System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        installDate = dt;
                    }
                }

                apps.Add(new InstalledApp(name, version, publisher, installDate));
            }

            if (apps.Count > 100)
            {
                apps = apps.Take(100).ToList();
                warnings.Add(new CollectionWarning(Name, "InstalledApps", "Limited to first 100 applications", null));
            }

            return apps;
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "InstalledApps", "Failed to query installed apps (Win32_Product is slow)", ex.Message));
            return Array.Empty<InstalledApp>();
        }
    }

    private IReadOnlyList<ProcessInfo>? CollectRunningProcesses(List<CollectionWarning> warnings)
    {
        try
        {
            var processes = new List<ProcessInfo>();
            var allProcesses = Process.GetProcesses();

            foreach (var proc in allProcesses)
            {
                try
                {
                    string? path = null;
                    string? userName = null;
                    
                    try
                    {
                        path = proc.MainModule?.FileName;
                    }
                    catch
                    {
                        // May not have access to path
                    }

                    try
                    {
                        userName = Environment.UserName;
                    }
                    catch
                    {
                        // Ignore
                    }

                    processes.Add(new ProcessInfo(
                        proc.Id,
                        proc.ProcessName,
                        path,
                        userName,
                        proc.WorkingSet64));
                }
                catch
                {
                    // Process may have exited or be inaccessible
                }
                finally
                {
                    proc.Dispose();
                }
            }

            if (processes.Count > 100)
            {
                processes = processes.Take(100).ToList();
                warnings.Add(new CollectionWarning(Name, "RunningProcesses", "Limited to first 100 processes", null));
            }

            return processes;
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "RunningProcesses", "Failed to enumerate processes", ex.Message));
            return null;
        }
    }
}
