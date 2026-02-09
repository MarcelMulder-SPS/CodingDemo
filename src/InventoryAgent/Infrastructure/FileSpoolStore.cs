using System.Text.Json;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryAgent.Infrastructure;

/// <summary>
/// File-based implementation of spool store using atomic file operations.
/// </summary>
public class FileSpoolStore : ISpoolStore
{
    private readonly SpoolOptions _options;
    private readonly ILogger<FileSpoolStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false
    };

    public FileSpoolStore(IOptions<SpoolOptions> options, ILogger<FileSpoolStore> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        // Ensure spool directory exists
        Directory.CreateDirectory(_options.DirectoryPath);
    }

    public async Task EnqueueAsync(InventoryReport report, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}.json";
            var filePath = Path.Combine(_options.DirectoryPath, fileName);
            var tempPath = filePath + ".tmp";

            // Write to temp file first (atomic operation)
            var json = JsonSerializer.Serialize(report, _jsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            // Atomic rename
            File.Move(tempPath, filePath, overwrite: false);

            _logger.LogDebug("Enqueued report to {FilePath}", filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<InventoryReport>> DequeueAsync(int maxCount, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var files = Directory.GetFiles(_options.DirectoryPath, "*.json")
                .OrderBy(f => f)
                .Take(maxCount)
                .ToList();

            var reports = new List<InventoryReport>();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var report = JsonSerializer.Deserialize<InventoryReport>(json);
                    if (report != null)
                    {
                        reports.Add(report);
                        File.Delete(file);
                        _logger.LogDebug("Dequeued report from {FilePath}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dequeue report from {FilePath}", file);
                }
            }

            return reports;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<int> GetQueuedCountAsync(CancellationToken cancellationToken)
    {
        var count = Directory.GetFiles(_options.DirectoryPath, "*.json").Length;
        return Task.FromResult(count);
    }
}
