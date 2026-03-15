namespace CentralMonitoring.Agent;

public class AgentOptions
{
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:5188";
    public string ApiKey { get; set; } = "dev-api-key";
    public Guid HostId { get; set; }
    public int FlushIntervalSeconds { get; set; } = 15;
    public int RequestTimeoutSeconds { get; set; } = 10;

    // Local buffer
    public string BufferFilePath { get; set; } = "buffer/agent-buffer.json";
    public int MaxBufferedBatches { get; set; } = 500;

    // Backoff
    public int BackoffInitialSeconds { get; set; } = 2;
    public int BackoffMaxSeconds { get; set; } = 60;
    public double BackoffMultiplier { get; set; } = 2.0;
    public double BackoffJitterRatio { get; set; } = 0.2;

    // Extra collectors
    public int TopProcessesCount { get; set; } = 5;
    public List<string> CriticalWindowsServices { get; set; } = new();
    public List<string> CriticalLinuxSystemdUnits { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
            errors.Add("Agent:ApiBaseUrl invalida.");

        if (HostId == Guid.Empty)
            errors.Add("Agent:HostId es obligatorio y no puede ser Guid.Empty.");

        if (string.IsNullOrWhiteSpace(ApiKey))
            errors.Add("Agent:ApiKey es obligatoria.");

        if (FlushIntervalSeconds < 5 || FlushIntervalSeconds > 3600)
            errors.Add("Agent:FlushIntervalSeconds debe estar entre 5 y 3600.");

        if (RequestTimeoutSeconds < 2 || RequestTimeoutSeconds > 300)
            errors.Add("Agent:RequestTimeoutSeconds debe estar entre 2 y 300.");

        if (MaxBufferedBatches < 10 || MaxBufferedBatches > 100000)
            errors.Add("Agent:MaxBufferedBatches debe estar entre 10 y 100000.");

        if (BackoffInitialSeconds < 1 || BackoffInitialSeconds > 300)
            errors.Add("Agent:BackoffInitialSeconds debe estar entre 1 y 300.");

        if (BackoffMaxSeconds < BackoffInitialSeconds || BackoffMaxSeconds > 3600)
            errors.Add("Agent:BackoffMaxSeconds debe ser >= BackoffInitialSeconds y <= 3600.");

        if (BackoffMultiplier < 1.1 || BackoffMultiplier > 10)
            errors.Add("Agent:BackoffMultiplier debe estar entre 1.1 y 10.");

        if (BackoffJitterRatio < 0 || BackoffJitterRatio > 0.5)
            errors.Add("Agent:BackoffJitterRatio debe estar entre 0 y 0.5.");

        if (string.IsNullOrWhiteSpace(BufferFilePath))
            errors.Add("Agent:BufferFilePath es obligatoria.");

        if (TopProcessesCount < 1 || TopProcessesCount > 50)
            errors.Add("Agent:TopProcessesCount debe estar entre 1 y 50.");

        return errors;
    }
}
