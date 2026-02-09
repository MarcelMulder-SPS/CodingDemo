namespace InventoryAgent;

public class InventoryOptions
{
    public int CollectionIntervalSeconds { get; set; } = 3600;
    public bool EnableParallelCollection { get; set; } = true;
}

public class UploadOptions
{
    public string Endpoint { get; set; } = "http://localhost:5000/api/inventory";
    public int MaxBatchSize { get; set; } = 10;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 30;
    public double BackoffMultiplier { get; set; } = 2.0;
}

public class SpoolOptions
{
    public string DirectoryPath { get; set; } = "./spool";
    public int MaxQueueSize { get; set; } = 1000;
}

public class LoggingOptions
{
    public bool Verbose { get; set; } = false;
}
