using System.Net.NetworkInformation;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace InventoryAgent.Collectors.macOS;

public class MacOSHardwareCollector : ICollector
{
    private readonly ILogger<MacOSHardwareCollector> _logger;

    public string Name => "MacOSHardwareCollector";

    public MacOSHardwareCollector(ILogger<MacOSHardwareCollector> logger)
    {
        _logger = logger;
    }

    public async Task<(object? Data, IReadOnlyList<CollectionWarning> Warnings)> CollectAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<CollectionWarning>();

        // On macOS, most hardware details require command execution which is not allowed
        warnings.Add(new CollectionWarning(Name, "CPU", "CPU details not available via managed APIs on macOS", null));
        warnings.Add(new CollectionWarning(Name, "Memory", "Memory details not available via managed APIs on macOS", null));
        warnings.Add(new CollectionWarning(Name, "Disks", "Disk details not available via managed APIs on macOS", null));
        warnings.Add(new CollectionWarning(Name, "GPU", "GPU details not available via managed APIs on macOS", null));
        warnings.Add(new CollectionWarning(Name, "Motherboard", "Motherboard details not available via managed APIs on macOS", null));
        warnings.Add(new CollectionWarning(Name, "BIOS", "BIOS details not available via managed APIs on macOS", null));
        warnings.Add(new CollectionWarning(Name, "Monitors", "Monitor details not available via managed APIs on macOS", null));

        var volumes = CollectVolumeInfo(warnings);
        var networkAdapters = CollectNetworkAdapters(warnings);

        var hardware = new HardwareInfo(
            null, // CPU
            null, // Memory
            Array.Empty<DiskInfo>(),
            volumes,
            Array.Empty<GpuInfo>(),
            networkAdapters,
            null, // Motherboard
            null, // BIOS
            Array.Empty<MonitorInfo>());

        return (hardware, warnings);
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
}
