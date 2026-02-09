using System.Runtime.InteropServices;
using InventoryAgent;
using InventoryAgent.Collectors.Linux;
using InventoryAgent.Collectors.macOS;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Infrastructure;
using InventoryAgent.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure options from appsettings.json
builder.Services.Configure<InventoryOptions>(builder.Configuration.GetSection("Inventory"));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Upload"));
builder.Services.Configure<SpoolOptions>(builder.Configuration.GetSection("Spool"));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("Logging"));

// Register infrastructure services
builder.Services.AddSingleton<ISpoolStore, FileSpoolStore>();
builder.Services.AddSingleton<IUploadCoordinator, UploadCoordinator>();
builder.Services.AddSingleton<InventoryOrchestrator>();

// Register HttpClientFactory
builder.Services.AddHttpClient();

// Register platform-specific collectors
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
#if WINDOWS
    // Only available when building on Windows
    builder.Services.AddWindowsCollectors();
#else
    throw new PlatformNotSupportedException("Windows collectors require building on Windows");
#endif
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddSingleton<ICollector, LinuxHardwareCollector>();
    builder.Services.AddSingleton<ICollector, LinuxSoftwareCollector>();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    builder.Services.AddSingleton<ICollector, MacOSHardwareCollector>();
    builder.Services.AddSingleton<ICollector, MacOSSoftwareCollector>();
}
else
{
    throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");
}

// Register the worker service
builder.Services.AddHostedService<Worker>();

// Configure platform-specific host behavior
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "InventoryAgent";
    });
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddSystemd();
}

var host = builder.Build();
host.Run();
