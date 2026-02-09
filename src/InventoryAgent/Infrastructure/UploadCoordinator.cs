using System.Net.Http.Json;
using System.Text.Json;
using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryAgent.Infrastructure;

/// <summary>
/// Coordinates the upload of spooled reports with retry and backoff logic.
/// </summary>
public class UploadCoordinator : IUploadCoordinator
{
    private readonly ISpoolStore _spoolStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UploadOptions _options;
    private readonly ILogger<UploadCoordinator> _logger;

    public UploadCoordinator(
        ISpoolStore spoolStore,
        IHttpClientFactory httpClientFactory,
        IOptions<UploadOptions> options,
        ILogger<UploadCoordinator> logger)
    {
        _spoolStore = spoolStore;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task FlushSpoolAsync(CancellationToken cancellationToken)
    {
        try
        {
            var queuedCount = await _spoolStore.GetQueuedCountAsync(cancellationToken);
            if (queuedCount == 0)
            {
                _logger.LogDebug("No reports to upload");
                return;
            }

            _logger.LogInformation("Starting upload of {Count} queued reports", queuedCount);

            var reports = await _spoolStore.DequeueAsync(_options.MaxBatchSize, cancellationToken);
            if (reports.Count == 0)
            {
                return;
            }

            await UploadBatchWithRetryAsync(reports, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing spool");
        }
    }

    private async Task UploadBatchWithRetryAsync(IReadOnlyList<InventoryReport> reports, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);

        for (int attempt = 0; attempt <= _options.RetryCount; attempt++)
        {
            try
            {
                _logger.LogDebug("Upload attempt {Attempt} of {MaxAttempts} for batch of {Count} reports",
                    attempt + 1, _options.RetryCount + 1, reports.Count);

                var response = await httpClient.PostAsJsonAsync(_options.Endpoint, reports, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Successfully uploaded {Count} reports", reports.Count);
                return;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == _options.RetryCount)
                {
                    _logger.LogError(ex, "Failed to upload batch after {Attempts} attempts. Re-spooling reports.", attempt + 1);
                    await ReSpoolReportsAsync(reports, cancellationToken);
                    throw;
                }

                _logger.LogWarning(ex, "Upload attempt {Attempt} failed. Retrying after {Delay}s",
                    attempt + 1, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * _options.BackoffMultiplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during upload. Re-spooling reports.");
                await ReSpoolReportsAsync(reports, cancellationToken);
                throw;
            }
        }
    }

    private async Task ReSpoolReportsAsync(IReadOnlyList<InventoryReport> reports, CancellationToken cancellationToken)
    {
        foreach (var report in reports)
        {
            try
            {
                await _spoolStore.EnqueueAsync(report, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-spool report for {Hostname} at {Timestamp}",
                    report.Hostname, report.Timestamp);
            }
        }
    }
}
