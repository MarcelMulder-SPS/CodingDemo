using System.Diagnostics;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace InventoryAgent.Collectors.macOS;

public class MacOSSoftwareCollector : ICollector
{
    private readonly ILogger<MacOSSoftwareCollector> _logger;

    public string Name => "MacOSSoftwareCollector";

    public MacOSSoftwareCollector(ILogger<MacOSSoftwareCollector> logger)
    {
        _logger = logger;
    }

    public async Task<(object? Data, IReadOnlyList<CollectionWarning> Warnings)> CollectAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<CollectionWarning>();

        var os = CollectOsInfo(warnings);
        
        warnings.Add(new CollectionWarning(Name, "InstalledApps", "App enumeration not available via managed APIs on macOS", null));
        
        var runningProcesses = CollectRunningProcesses(warnings);

        var software = new SoftwareInfo(os, Array.Empty<InstalledApp>(), runningProcesses);

        return (software, warnings);
    }

    private OsInfo? CollectOsInfo(List<CollectionWarning> warnings)
    {
        try
        {
            var name = "macOS";
            var version = Environment.OSVersion.Version.ToString();
            var architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";

            warnings.Add(new CollectionWarning(Name, "OS.Details", "Detailed OS info requires system_profiler which is not allowed", null));

            return new OsInfo(
                name,
                version,
                null, // Build
                null, // Kernel version
                architecture,
                null, // Boot time
                null); // Uptime
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "OS", "Failed to read OS info", ex.Message));
            return null;
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
                    string? userName = null;
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
                        null,
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
