# Inventory Agent

A production-quality .NET 8 cross-platform background service that inventories hardware and software information and uploads reports to a configured HTTP endpoint.

## Architecture

This solution consists of three projects:

### 1. InventoryAgent.Contracts (net8.0)
Domain models and interfaces shared across the solution.
- **Models**: Immutable records for hardware/software inventory data
- **Interfaces**: Contracts for collectors, spool store, and upload coordinator

### 2. InventoryAgent (net8.0)
Cross-platform worker service that runs on Windows, Linux, and macOS.
- **Program.cs**: Composition root with dependency injection
- **Worker**: Background service using PeriodicTimer for scheduled collection
- **InventoryOrchestrator**: Parallel collection from all registered collectors
- **UploadCoordinator**: Batch upload with retry/backoff logic
- **FileSpoolStore**: Atomic file-based queue for offline-first operation
- **Linux Collectors**: File-based collection from /proc, /sys, /etc
- **macOS Collectors**: Limited collection via managed APIs

### 3. InventoryAgent.Collectors.Windows (net8.0-windows)
Windows-specific collectors using System.Management (WMI/CIM).
- **WindowsHardwareCollector**: CPU, Memory, Disks, GPU, Network, Motherboard, BIOS, Monitors
- **WindowsSoftwareCollector**: OS info, Installed apps, Running processes
- **AddWindowsCollectors extension**: DI registration helper

## Platform Support

The solution uses runtime platform detection to register appropriate collectors:

- **Windows**: System.Management (WMI) for comprehensive hardware/software inventory
- **Linux**: File reads from /proc, /sys, /etc + managed APIs (NetworkInterface, DriveInfo)
- **macOS**: Managed APIs only (limited hardware details available)

Windows collectors are conditionally referenced and built only on Windows hosts.

## Features

✅ **Offline-first**: Reports are always spooled to disk before upload  
✅ **Batch upload**: Configurable batch size (default: 10 reports per POST)  
✅ **Retry with backoff**: Exponential backoff for failed uploads  
✅ **Best-effort collection**: Per-collector failures don't stop the whole scan  
✅ **Warnings model**: Detailed warnings for missing/unavailable data  
✅ **Parallel collection**: Collectors run in parallel (configurable)  
✅ **Service mode**: Windows Service (UseWindowsService) or systemd (UseSystemd)  
✅ **Console mode**: Also runnable as a normal console process  
✅ **Verbose logging**: Configurable via appsettings.json  
✅ **No command execution**: Pure managed APIs and file reads only  

## Configuration

Edit `appsettings.json`:

```json
{
  "Inventory": {
    "CollectionIntervalSeconds": 3600,
    "EnableParallelCollection": true
  },
  "Upload": {
    "Endpoint": "http://localhost:5000/api/inventory",
    "MaxBatchSize": 10,
    "RetryCount": 3,
    "RetryDelaySeconds": 30,
    "BackoffMultiplier": 2.0
  },
  "Spool": {
    "DirectoryPath": "./spool",
    "MaxQueueSize": 1000
  },
  "Logging": {
    "Verbose": false
  }
}
```

## Building

```bash
# Build the solution (Linux/macOS)
dotnet build InventoryAgent.sln

# Build on Windows (includes Windows collectors)
dotnet build InventoryAgent.sln
```

## Running

### As a console application:
```bash
cd src/InventoryAgent
dotnet run
```

### As a Windows Service:
```powershell
# Publish
dotnet publish -c Release -o ./publish

# Install service (requires admin)
sc create InventoryAgent binPath="C:\path\to\publish\InventoryAgent.exe"
sc start InventoryAgent
```

### As a Linux systemd service:
```bash
# Publish
dotnet publish -c Release -o ./publish

# Create systemd unit file
sudo nano /etc/systemd/system/inventoryagent.service

# Example unit file content:
# [Unit]
# Description=Inventory Agent
# After=network.target
#
# [Service]
# Type=notify
# WorkingDirectory=/opt/inventoryagent
# ExecStart=/usr/bin/dotnet /opt/inventoryagent/InventoryAgent.dll
# Restart=always
#
# [Install]
# WantedBy=multi-user.target

# Enable and start
sudo systemctl enable inventoryagent
sudo systemctl start inventoryagent
```

## Data Collection

### Hardware
- **CPU**: Model, vendor, cores, threads, architecture, frequency
- **Memory**: Total size, module breakdown (Windows only)
- **Disks**: Model, size, interface, serial (best effort)
- **Volumes**: All mounted volumes with free/used space
- **GPU**: Model, vendor, driver, VRAM (Windows only)
- **Network**: Adapters with MAC, IPv4/IPv6, link speed
- **Motherboard**: Vendor, model, version, serial (Windows only)
- **BIOS**: Vendor, version, release date (Windows only)
- **Monitors**: Best effort (Windows only)

### Software
- **OS**: Name, version, build, kernel, architecture, uptime, boot time
- **Installed Apps**: Best effort (Windows only)
- **Running Processes**: Limited to first 100

## Warnings

Collectors emit warnings for unavailable data:
- Field-level warnings with collector name, field, message, and optional exception
- Aggregated into `Report.Warnings` array
- Allows monitoring of collection quality

## Privacy & Security

- **Machine identity**: Hostname only (no stable hardware hash)
- **Privacy**: Includes MAC addresses, serial numbers, usernames, installed apps
- **No authentication**: Upload endpoint does not use authentication
- **No command execution**: Pure managed APIs for security
- **No P/Invoke**: Windows uses System.Management only

## Architecture Principles

- **Options pattern**: IOptions<T> for configuration
- **IHttpClientFactory**: For HTTP client management
- **Dependency injection**: All services, collectors registered in DI
- **SOLID principles**: Single responsibility, interface segregation
- **DRY**: Shared utilities for parsing and mapping
- **Immutable records**: Domain models are immutable
- **Extension points**: Adding collectors doesn't require orchestration changes

## License

Copyright © 2026
