using InventoryAgent.Collectors.Windows;
using InventoryAgent.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryAgent.Collectors.Windows;

/// <summary>
/// Extension methods for registering Windows-specific collectors.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsCollectors(this IServiceCollection services)
    {
        services.AddSingleton<ICollector, WindowsHardwareCollector>();
        services.AddSingleton<ICollector, WindowsSoftwareCollector>();
        
        return services;
    }
}
