using System.Diagnostics;
using System.Text.RegularExpressions;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace InventoryAgent.Collectors.Linux;

public class LinuxSoftwareCollector : ICollector
{
    private readonly ILogger<LinuxSoftwareCollector> _logger;

    public string Name => "LinuxSoftwareCollector";

    public LinuxSoftwareCollector(ILogger<LinuxSoftwareCollector> logger)
    {
        _logger = logger;
    }

    public async Task<(object? Data, IReadOnlyList<CollectionWarning> Warnings)> CollectAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<CollectionWarning>();

        var os = await CollectOsInfoAsync(warnings, cancellationToken);
        var installedApps = CollectInstalledApps(warnings);
        var runningProcesses = CollectRunningProcesses(warnings);

        var software = new SoftwareInfo(os, installedApps, runningProcesses);

        return (software, warnings);
    }

    private async Task<OsInfo?> CollectOsInfoAsync(List<CollectionWarning> warnings, CancellationToken cancellationToken)
    {
        try
        {
            string? name = null;
            string? version = null;
            string? build = null;

            var osReleasePath = "/etc/os-release";
            if (File.Exists(osReleasePath))
            {
                var lines = await File.ReadAllLinesAsync(osReleasePath, cancellationToken);
                foreach (var line in lines)
                {
                    if (line.StartsWith("PRETTY_NAME="))
                    {
                        name = line.Split('=', 2)[1].Trim('"');
                    }
                    else if (line.StartsWith("VERSION_ID="))
                    {
                        version = line.Split('=', 2)[1].Trim('"');
                    }
                    else if (line.StartsWith("BUILD_ID="))
                    {
                        build = line.Split('=', 2)[1].Trim('"');
                    }
                }
            }

            string? kernelVersion = null;
            var unameReleasePath = "/proc/sys/kernel/osrelease";
            if (File.Exists(unameReleasePath))
            {
                kernelVersion = (await File.ReadAllTextAsync(unameReleasePath, cancellationToken)).Trim();
            }

            var architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";

            DateTime? bootTime = null;
            TimeSpan? uptime = null;
            var uptimePath = "/proc/uptime";
            if (File.Exists(uptimePath))
            {
                var uptimeContent = await File.ReadAllTextAsync(uptimePath, cancellationToken);
                var uptimeMatch = Regex.Match(uptimeContent, @"^(\d+\.\d+)");
                if (uptimeMatch.Success && double.TryParse(uptimeMatch.Groups[1].Value, out var uptimeSeconds))
                {
                    uptime = TimeSpan.FromSeconds(uptimeSeconds);
                    bootTime = DateTime.UtcNow - uptime.Value;
                }
            }

            return new OsInfo(name, version, build, kernelVersion, architecture, bootTime, uptime);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "OS", "Failed to read OS info", ex.Message));
            return null;
        }
    }

    private IReadOnlyList<InstalledApp>? CollectInstalledApps(List<CollectionWarning> warnings)
    {
        warnings.Add(new CollectionWarning(Name, "InstalledApps", "Package enumeration requires distro-specific package manager access", null));
        return Array.Empty<InstalledApp>();
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
                        // Best effort - may not be accessible for all processes
                        userName = Environment.UserName;
                    }
                    catch
                    {
                        // Ignore
                    }

                    processes.Add(new ProcessInfo(
                        proc.Id,
                        proc.ProcessName,
                        null, // Path not easily accessible
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
