# Testing Guide

This guide explains how to test the Inventory Agent solution.

## Quick Start

### 1. Build the Solution

```bash
# On Linux/macOS
dotnet build InventoryAgent.sln

# On Windows (includes Windows collectors)
dotnet build InventoryAgent.sln
```

### 2. Run as Console Application

```bash
cd src/InventoryAgent
dotnet run
```

The application will:
1. Collect hardware and software inventory immediately on startup
2. Spool the report to `./spool/` directory
3. Attempt to upload to the configured endpoint
4. Wait for the next collection interval (default: 3600 seconds)

### 3. Test Offline Mode

The application is offline-first, so it works even without network connectivity:

1. Configure a non-existent endpoint in `appsettings.json`:
   ```json
   "Upload": {
     "Endpoint": "http://localhost:9999/api/inventory"
   }
   ```

2. Run the application - it will spool reports to disk

3. Check the spool directory:
   ```bash
   ls -l ./spool/
   cat ./spool/*.json | python3 -m json.tool
   ```

4. Once you fix the endpoint configuration, the next run will upload all spooled reports

### 4. Test Configuration

Edit `appsettings.Development.json` for testing:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    },
    "Verbose": true
  },
  "Inventory": {
    "CollectionIntervalSeconds": 60
  }
}
```

Run with:
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

### 5. Inspect Collected Data

The JSON report includes:
- Timestamp
- Hostname
- Hardware information (CPU, memory, disks, network, etc.)
- Software information (OS, processes, installed apps)
- Warnings for unavailable data

Example report structure:
```json
{
  "Timestamp": "2026-02-09T10:00:00Z",
  "Hostname": "my-machine",
  "Hardware": {
    "Cpu": {
      "Model": "Intel Core i7",
      "Cores": 4,
      "LogicalProcessors": 8
    },
    "Memory": {
      "TotalBytes": 17179869184
    },
    "Volumes": [
      {
        "Name": "/",
        "FileSystem": "ext4",
        "TotalBytes": 107374182400,
        "FreeBytes": 53687091200
      }
    ]
  },
  "Warnings": [
    {
      "CollectorName": "LinuxHardwareCollector",
      "Field": "GPU",
      "Message": "GPU detection not available via managed APIs on Linux"
    }
  ]
}
```

## Platform-Specific Testing

### Windows

On Windows, the collectors use WMI to gather comprehensive hardware information:

```powershell
cd src\InventoryAgent
dotnet run
```

Expected data:
- Full CPU details (model, vendor, cores, frequency)
- Memory modules breakdown
- Disk drive details (model, size, interface, serial)
- GPU information (model, vendor, VRAM, driver)
- Motherboard and BIOS details
- Installed applications
- Running processes

### Linux

On Linux, collectors use `/proc`, `/sys`, and managed APIs:

```bash
cd src/InventoryAgent
dotnet run
```

Expected data:
- CPU info from `/proc/cpuinfo`
- Memory total from `/proc/meminfo`
- OS details from `/etc/os-release`
- Network adapters via `NetworkInterface`
- Mounted volumes via `DriveInfo`
- Running processes

Expected warnings:
- "Disk details require sysfs parsing"
- "GPU detection not available"
- "Motherboard info requires DMI/SMBIOS access"
- "Package enumeration requires distro-specific package manager"

### macOS

On macOS, collectors are limited to managed APIs only:

```bash
cd src/InventoryAgent
dotnet run
```

Expected data:
- Network adapters
- Mounted volumes
- Running processes
- Basic OS info

Expected warnings:
- Most hardware details will emit warnings

## Service Installation Testing

### Windows Service

```powershell
# Publish
dotnet publish -c Release -o .\publish

# Install (as Administrator)
sc create InventoryAgent binPath="C:\path\to\publish\InventoryAgent.exe"

# Start service
sc start InventoryAgent

# Check status
sc query InventoryAgent

# View logs (Event Viewer or configured logging)

# Stop and remove
sc stop InventoryAgent
sc delete InventoryAgent
```

### Linux systemd Service

```bash
# Publish
dotnet publish -c Release -o ./publish

# Copy to /opt
sudo mkdir -p /opt/inventoryagent
sudo cp -r publish/* /opt/inventoryagent/

# Create systemd unit
sudo tee /etc/systemd/system/inventoryagent.service << EOF
[Unit]
Description=Inventory Agent
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/inventoryagent
ExecStart=/usr/bin/dotnet /opt/inventoryagent/InventoryAgent.dll
Restart=always
RestartSec=10
User=inventoryagent

[Install]
WantedBy=multi-user.target
EOF

# Create service user
sudo useradd -r -s /bin/false inventoryagent
sudo chown -R inventoryagent:inventoryagent /opt/inventoryagent

# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable inventoryagent
sudo systemctl start inventoryagent

# Check status
sudo systemctl status inventoryagent

# View logs
sudo journalctl -u inventoryagent -f

# Stop and disable
sudo systemctl stop inventoryagent
sudo systemctl disable inventoryagent
```

## Troubleshooting

### Application doesn't start
- Check .NET 8 Runtime is installed: `dotnet --version`
- Verify configuration file is valid JSON
- Check file permissions on spool directory

### No data collected
- Check logs for collector errors
- Verify platform-specific requirements
- Review warnings in the report

### Upload fails
- Verify endpoint is accessible: `curl -X POST http://your-endpoint/api/inventory`
- Check network connectivity
- Review retry settings in configuration
- Verify reports are being spooled to disk

### High CPU usage
- Disable parallel collection in configuration
- Increase collection interval
- Check for slow collectors in logs

### Permission errors on Linux
- Ensure user has read access to `/proc`, `/sys`, `/etc`
- Run with appropriate permissions for network/process enumeration
- Some data may require root (will emit warnings)

## Performance Testing

Monitor performance during collection:

```bash
# CPU and memory usage
top -p $(pgrep -f InventoryAgent)

# Disk I/O
iostat -x 1

# Network I/O
iftop
```

Expected performance:
- Collection time: < 5 seconds (varies by platform)
- Memory usage: < 100 MB
- CPU usage: < 10% during collection, ~0% idle
- Disk I/O: Minimal (only during spool/upload)

## CI/CD Testing

The solution can be tested in CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Build
  run: dotnet build InventoryAgent.sln
  
- name: Test
  run: |
    cd src/InventoryAgent
    timeout 10 dotnet run || true
    ls -la ./spool/
```

Note: Windows collectors project will only build on Windows runners.
