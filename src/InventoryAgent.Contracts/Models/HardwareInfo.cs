namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Aggregated hardware information.
/// </summary>
public record HardwareInfo(
    CpuInfo? Cpu,
    MemoryInfo? Memory,
    IReadOnlyList<DiskInfo>? Disks,
    IReadOnlyList<VolumeInfo>? Volumes,
    IReadOnlyList<GpuInfo>? Gpus,
    IReadOnlyList<NetworkAdapterInfo>? NetworkAdapters,
    MotherboardInfo? Motherboard,
    BiosInfo? Bios,
    IReadOnlyList<MonitorInfo>? Monitors);
