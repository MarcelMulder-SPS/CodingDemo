using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace InventoryAgent.Collectors.Linux;

public class LinuxHardwareCollector : ICollector
{
    private readonly ILogger<LinuxHardwareCollector> _logger;

    public string Name => "LinuxHardwareCollector";

    public LinuxHardwareCollector(ILogger<LinuxHardwareCollector> logger)
    {
        _logger = logger;
    }

    public async Task<(object? Data, IReadOnlyList<CollectionWarning> Warnings)> CollectAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<CollectionWarning>();

        var cpu = await CollectCpuInfoAsync(warnings, cancellationToken);
        var memory = await CollectMemoryInfoAsync(warnings, cancellationToken);
        var disks = await CollectDiskInfoAsync(warnings, cancellationToken);
        var volumes = CollectVolumeInfo(warnings);
        var gpus = CollectGpuInfo(warnings);
        var networkAdapters = CollectNetworkAdapters(warnings);
        var motherboard = CollectMotherboardInfo(warnings);
        var bios = CollectBiosInfo(warnings);
        var monitors = CollectMonitorInfo(warnings);

        var hardware = new HardwareInfo(
            cpu,
            memory,
            disks,
            volumes,
            gpus,
            networkAdapters,
            motherboard,
            bios,
            monitors);

        return (hardware, warnings);
    }

    private async Task<CpuInfo?> CollectCpuInfoAsync(List<CollectionWarning> warnings, CancellationToken cancellationToken)
    {
        try
        {
            var cpuinfoPath = "/proc/cpuinfo";
            if (!File.Exists(cpuinfoPath))
            {
                warnings.Add(new CollectionWarning(Name, "CPU", "File not found: /proc/cpuinfo", null));
                return null;
            }

            var lines = await File.ReadAllLinesAsync(cpuinfoPath, cancellationToken);
            
            string? model = null;
            string? vendor = null;
            double? frequencyMhz = null;
            var physicalIds = new HashSet<string>();
            var processorCount = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("model name"))
                {
                    model = line.Split(':', 2)[1].Trim();
                }
                else if (line.StartsWith("vendor_id"))
                {
                    vendor = line.Split(':', 2)[1].Trim();
                }
                else if (line.StartsWith("cpu MHz"))
                {
                    if (double.TryParse(line.Split(':', 2)[1].Trim(), out var mhz))
                    {
                        frequencyMhz = mhz;
                    }
                }
                else if (line.StartsWith("physical id"))
                {
                    physicalIds.Add(line.Split(':', 2)[1].Trim());
                }
                else if (line.StartsWith("processor"))
                {
                    processorCount++;
                }
            }

            var architecture = RuntimeInformation.ProcessArchitecture.ToString();

            return new CpuInfo(
                model,
                vendor,
                physicalIds.Count > 0 ? physicalIds.Count : null,
                processorCount > 0 ? processorCount : null,
                architecture,
                frequencyMhz);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "CPU", "Failed to read CPU info", ex.Message));
            return null;
        }
    }

    private async Task<MemoryInfo?> CollectMemoryInfoAsync(List<CollectionWarning> warnings, CancellationToken cancellationToken)
    {
        try
        {
            var meminfoPath = "/proc/meminfo";
            if (!File.Exists(meminfoPath))
            {
                warnings.Add(new CollectionWarning(Name, "Memory", "File not found: /proc/meminfo", null));
                return null;
            }

            var lines = await File.ReadAllLinesAsync(meminfoPath, cancellationToken);
            long? totalBytes = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    var match = Regex.Match(line, @"(\d+)\s+kB");
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var kb))
                    {
                        totalBytes = kb * 1024;
                        break;
                    }
                }
            }

            warnings.Add(new CollectionWarning(Name, "Memory.Modules", "Module breakdown not available on Linux without root access", null));

            return new MemoryInfo(totalBytes, null);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "Memory", "Failed to read memory info", ex.Message));
            return null;
        }
    }

    private async Task<IReadOnlyList<DiskInfo>?> CollectDiskInfoAsync(List<CollectionWarning> warnings, CancellationToken cancellationToken)
    {
        warnings.Add(new CollectionWarning(Name, "Disks", "Disk details require sysfs parsing which is best-effort on Linux", null));
        return Array.Empty<DiskInfo>();
    }

    private IReadOnlyList<VolumeInfo>? CollectVolumeInfo(List<CollectionWarning> warnings)
    {
        try
        {
            var volumes = new List<VolumeInfo>();
            var drives = DriveInfo.GetDrives();

            foreach (var drive in drives)
            {
                try
                {
                    if (drive.IsReady)
                    {
                        var totalBytes = drive.TotalSize;
                        var freeBytes = drive.AvailableFreeSpace;
                        var usedBytes = totalBytes - freeBytes;

                        volumes.Add(new VolumeInfo(
                            drive.Name,
                            drive.DriveFormat,
                            totalBytes,
                            freeBytes,
                            usedBytes));
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add(new CollectionWarning(Name, $"Volume.{drive.Name}", "Failed to read volume info", ex.Message));
                }
            }

            return volumes;
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "Volumes", "Failed to enumerate volumes", ex.Message));
            return null;
        }
    }

    private IReadOnlyList<GpuInfo>? CollectGpuInfo(List<CollectionWarning> warnings)
    {
        warnings.Add(new CollectionWarning(Name, "GPU", "GPU detection not available via managed APIs on Linux", null));
        return Array.Empty<GpuInfo>();
    }

    private IReadOnlyList<NetworkAdapterInfo>? CollectNetworkAdapters(List<CollectionWarning> warnings)
    {
        try
        {
            var adapters = new List<NetworkAdapterInfo>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var iface in interfaces)
            {
                try
                {
                    var macAddress = iface.GetPhysicalAddress().ToString();
                    if (string.IsNullOrEmpty(macAddress) || macAddress == "000000000000")
                    {
                        continue;
                    }

                    var props = iface.GetIPProperties();
                    var ipv4 = props.UnicastAddresses
                        .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(a => a.Address.ToString())
                        .ToList();
                    var ipv6 = props.UnicastAddresses
                        .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        .Select(a => a.Address.ToString())
                        .ToList();

                    long? linkSpeed = null;
                    try
                    {
                        linkSpeed = iface.Speed;
                    }
                    catch
                    {
                        // Speed not available for some interfaces
                    }

                    adapters.Add(new NetworkAdapterInfo(
                        iface.Name,
                        FormatMacAddress(macAddress),
                        ipv4,
                        ipv6,
                        linkSpeed));
                }
                catch (Exception ex)
                {
                    warnings.Add(new CollectionWarning(Name, $"NetworkAdapter.{iface.Name}", "Failed to read network adapter info", ex.Message));
                }
            }

            return adapters;
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "NetworkAdapters", "Failed to enumerate network adapters", ex.Message));
            return null;
        }
    }

    private string FormatMacAddress(string mac)
    {
        if (mac.Length == 12)
        {
            return string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
        }
        return mac;
    }

    private MotherboardInfo? CollectMotherboardInfo(List<CollectionWarning> warnings)
    {
        warnings.Add(new CollectionWarning(Name, "Motherboard", "Motherboard info requires DMI/SMBIOS access not available via managed APIs", null));
        return null;
    }

    private BiosInfo? CollectBiosInfo(List<CollectionWarning> warnings)
    {
        warnings.Add(new CollectionWarning(Name, "BIOS", "BIOS info requires DMI/SMBIOS access not available via managed APIs", null));
        return null;
    }

    private IReadOnlyList<MonitorInfo>? CollectMonitorInfo(List<CollectionWarning> warnings)
    {
        warnings.Add(new CollectionWarning(Name, "Monitors", "Monitor detection not available via managed APIs on Linux", null));
        return Array.Empty<MonitorInfo>();
    }
}
