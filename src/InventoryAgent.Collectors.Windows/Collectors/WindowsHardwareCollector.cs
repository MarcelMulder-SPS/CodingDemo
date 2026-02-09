using System.Management;
using System.Net.NetworkInformation;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace InventoryAgent.Collectors.Windows;

public class WindowsHardwareCollector : ICollector
{
    private readonly ILogger<WindowsHardwareCollector> _logger;

    public string Name => "WindowsHardwareCollector";

    public WindowsHardwareCollector(ILogger<WindowsHardwareCollector> logger)
    {
        _logger = logger;
    }

    public async Task<(object? Data, IReadOnlyList<CollectionWarning> Warnings)> CollectAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<CollectionWarning>();

        var cpu = await Task.Run(() => CollectCpuInfo(warnings), cancellationToken);
        var memory = await Task.Run(() => CollectMemoryInfo(warnings), cancellationToken);
        var disks = await Task.Run(() => CollectDiskInfo(warnings), cancellationToken);
        var volumes = CollectVolumeInfo(warnings);
        var gpus = await Task.Run(() => CollectGpuInfo(warnings), cancellationToken);
        var networkAdapters = CollectNetworkAdapters(warnings);
        var motherboard = await Task.Run(() => CollectMotherboardInfo(warnings), cancellationToken);
        var bios = await Task.Run(() => CollectBiosInfo(warnings), cancellationToken);
        var monitors = await Task.Run(() => CollectMonitorInfo(warnings), cancellationToken);

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

    private CpuInfo? CollectCpuInfo(List<CollectionWarning> warnings)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            var cpu = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            
            if (cpu == null)
            {
                warnings.Add(new CollectionWarning(Name, "CPU", "No CPU found via WMI", null));
                return null;
            }

            var model = cpu["Name"]?.ToString();
            var vendor = cpu["Manufacturer"]?.ToString();
            var cores = Convert.ToInt32(cpu["NumberOfCores"]);
            var logicalProcessors = Convert.ToInt32(cpu["NumberOfLogicalProcessors"]);
            var architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            var maxClockSpeed = Convert.ToDouble(cpu["MaxClockSpeed"]);

            return new CpuInfo(model, vendor, cores, logicalProcessors, architecture, maxClockSpeed);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "CPU", "Failed to query CPU info", ex.Message));
            return null;
        }
    }

    private MemoryInfo? CollectMemoryInfo(List<CollectionWarning> warnings)
    {
        try
        {
            long totalBytes = 0;
            var modules = new List<MemoryModuleInfo>();

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
            {
                foreach (ManagementObject mem in searcher.Get())
                {
                    var capacity = Convert.ToInt64(mem["Capacity"]);
                    totalBytes += capacity;

                    var manufacturer = mem["Manufacturer"]?.ToString();
                    var partNumber = mem["PartNumber"]?.ToString()?.Trim();
                    var speed = mem["Speed"] != null ? Convert.ToInt32(mem["Speed"]) : (int?)null;

                    modules.Add(new MemoryModuleInfo(capacity, manufacturer, partNumber, speed));
                }
            }

            return new MemoryInfo(totalBytes > 0 ? totalBytes : null, modules);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "Memory", "Failed to query memory info", ex.Message));
            return null;
        }
    }

    private IReadOnlyList<DiskInfo>? CollectDiskInfo(List<CollectionWarning> warnings)
    {
        try
        {
            var disks = new List<DiskInfo>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject disk in searcher.Get())
            {
                var model = disk["Model"]?.ToString();
                var sizeBytes = disk["Size"] != null ? Convert.ToInt64(disk["Size"]) : (long?)null;
                var interfaceType = disk["InterfaceType"]?.ToString();
                var serialNumber = disk["SerialNumber"]?.ToString()?.Trim();

                disks.Add(new DiskInfo(model, sizeBytes, interfaceType, serialNumber));
            }

            return disks;
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "Disks", "Failed to query disk info", ex.Message));
            return null;
        }
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
        try
        {
            var gpus = new List<GpuInfo>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject gpu in searcher.Get())
            {
                var model = gpu["Name"]?.ToString();
                var vendor = gpu["AdapterCompatibility"]?.ToString();
                var driverVersion = gpu["DriverVersion"]?.ToString();
                var vramBytes = gpu["AdapterRAM"] != null ? Convert.ToInt64(gpu["AdapterRAM"]) : (long?)null;

                gpus.Add(new GpuInfo(model, vendor, driverVersion, vramBytes));
            }

            return gpus;
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "GPU", "Failed to query GPU info", ex.Message));
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

    private MotherboardInfo? CollectMotherboardInfo(List<CollectionWarning> warnings)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            var board = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (board == null)
            {
                warnings.Add(new CollectionWarning(Name, "Motherboard", "No motherboard found via WMI", null));
                return null;
            }

            var manufacturer = board["Manufacturer"]?.ToString();
            var product = board["Product"]?.ToString();
            var version = board["Version"]?.ToString();
            var serialNumber = board["SerialNumber"]?.ToString();

            return new MotherboardInfo(manufacturer, product, version, serialNumber);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "Motherboard", "Failed to query motherboard info", ex.Message));
            return null;
        }
    }

    private BiosInfo? CollectBiosInfo(List<CollectionWarning> warnings)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            var bios = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (bios == null)
            {
                warnings.Add(new CollectionWarning(Name, "BIOS", "No BIOS found via WMI", null));
                return null;
            }

            var vendor = bios["Manufacturer"]?.ToString();
            var version = bios["SMBIOSBIOSVersion"]?.ToString();
            
            DateTime? releaseDate = null;
            if (bios["ReleaseDate"] != null)
            {
                var releaseDateStr = bios["ReleaseDate"].ToString();
                if (ManagementDateTimeConverter.ToDateTime(releaseDateStr!) is DateTime dt)
                {
                    releaseDate = dt;
                }
            }

            return new BiosInfo(vendor, version, releaseDate);
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "BIOS", "Failed to query BIOS info", ex.Message));
            return null;
        }
    }

    private IReadOnlyList<MonitorInfo>? CollectMonitorInfo(List<CollectionWarning> warnings)
    {
        try
        {
            var monitors = new List<MonitorInfo>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DesktopMonitor");
            foreach (ManagementObject monitor in searcher.Get())
            {
                var name = monitor["Name"]?.ToString();
                var manufacturer = monitor["MonitorManufacturer"]?.ToString();
                var model = monitor["MonitorType"]?.ToString();
                var width = monitor["ScreenWidth"] != null ? Convert.ToInt32(monitor["ScreenWidth"]) : (int?)null;
                var height = monitor["ScreenHeight"] != null ? Convert.ToInt32(monitor["ScreenHeight"]) : (int?)null;

                monitors.Add(new MonitorInfo(name, manufacturer, model, width, height));
            }

            if (monitors.Count == 0)
            {
                warnings.Add(new CollectionWarning(Name, "Monitors", "No monitors found via WMI (this is common)", null));
            }

            return monitors;
        }
        catch (Exception ex)
        {
            warnings.Add(new CollectionWarning(Name, "Monitors", "Failed to query monitor info", ex.Message));
            return null;
        }
    }
}
