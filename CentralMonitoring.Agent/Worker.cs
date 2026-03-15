using System.Net.Http.Json;
using CentralMonitoring.Shared.DTOs.Metrics;
using Microsoft.Extensions.Options;

namespace CentralMonitoring.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentOptions _options;
    private readonly SystemMetricsCollector _collector;
    private readonly LocalBatchBuffer _buffer;
    private readonly Random _random = new();

    public Worker(
        ILogger<Worker> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AgentOptions> options,
        LocalBatchBuffer buffer)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _collector = new SystemMetricsCollector(_options);
        _buffer = buffer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var validationErrors = _options.Validate();
        if (validationErrors.Count > 0)
        {
            foreach (var error in validationErrors)
                _logger.LogError("{ConfigError}", error);

            _logger.LogError("Configuracion invalida. El agente no iniciara envios.");
            return;
        }

        var baseUri = new Uri(_options.ApiBaseUrl);

        _buffer.Initialize(_options.BufferFilePath, _options.MaxBufferedBatches);
        var intervalSeconds = _options.FlushIntervalSeconds;
        _logger.LogInformation(
            "Agent iniciado. HostId={HostId} ApiBaseUrl={ApiBaseUrl} Interval={Interval}s Buffer={BufferPath} MaxBuffered={MaxBuffered}",
            _options.HostId,
            _options.ApiBaseUrl,
            intervalSeconds,
            _options.BufferFilePath,
            _options.MaxBufferedBatches);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTime.UtcNow;
            var request = BuildIngestRequest(nowUtc);
            var batch = new PendingIngestBatch
            {
                Payload = request,
                EnqueuedAtUtc = nowUtc,
                NextAttemptUtc = nowUtc
            };

            _buffer.TryEnqueue(batch, out var droppedOldest);
            if (droppedOldest)
            {
                _logger.LogWarning("Buffer lleno ({MaxBuffered}). Se descarto el batch mas antiguo para mantener continuidad.", _options.MaxBufferedBatches);
            }

            TimeSpan? suggestedDelay = null;
            try
            {
                suggestedDelay = await FlushPendingAsync(baseUri, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando metricas al central.");
                suggestedDelay = ComputeBackoff(_options.BackoffInitialSeconds, _options.BackoffMaxSeconds, _options.BackoffMultiplier, 1, _options.BackoffJitterRatio);
            }

            var nextDelay = suggestedDelay ?? TimeSpan.FromSeconds(intervalSeconds);

            _logger.LogInformation("Siguiente ciclo en {DelaySeconds}s. Pendientes={Pending}", (int)nextDelay.TotalSeconds, _buffer.Count);
            await Task.Delay(nextDelay, stoppingToken);
        }
    }

    private async Task<TimeSpan?> FlushPendingAsync(Uri baseUri, CancellationToken stoppingToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = baseUri;
        client.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", _options.ApiKey);

        var sentCount = 0;
        while (!stoppingToken.IsCancellationRequested && _buffer.TryPeek(out var pending) && pending is not null)
        {
            if (pending.NextAttemptUtc > DateTime.UtcNow)
            {
                var wait = pending.NextAttemptUtc - DateTime.UtcNow;
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }

            pending.AttemptCount += 1;
            pending.LastAttemptUtc = DateTime.UtcNow;
            _buffer.UpdateHead(pending);

            try
            {
                var response = await client.PostAsJsonAsync("/api/v1/metrics/ingest", pending.Payload, stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    _buffer.TryDequeue(out _);
                    sentCount += 1;
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(stoppingToken);
                var delay = ComputeBackoff(
                    _options.BackoffInitialSeconds,
                    _options.BackoffMaxSeconds,
                    _options.BackoffMultiplier,
                    pending.AttemptCount,
                    _options.BackoffJitterRatio);

                pending.NextAttemptUtc = DateTime.UtcNow.Add(delay);
                pending.LastError = $"HTTP {(int)response.StatusCode}: {body}";
                _buffer.UpdateHead(pending);

                _logger.LogWarning(
                    "Ingest fallo para batch {BatchId}. Status={StatusCode}. Reintento en {DelaySeconds}s. Pendientes={Pending}",
                    pending.BatchId,
                    (int)response.StatusCode,
                    (int)delay.TotalSeconds,
                    _buffer.Count);
                return delay;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var delay = ComputeBackoff(
                    _options.BackoffInitialSeconds,
                    _options.BackoffMaxSeconds,
                    _options.BackoffMultiplier,
                    pending.AttemptCount,
                    _options.BackoffJitterRatio);

                pending.NextAttemptUtc = DateTime.UtcNow.Add(delay);
                pending.LastError = ex.Message;
                _buffer.UpdateHead(pending);

                _logger.LogWarning(ex,
                    "Error de red para batch {BatchId}. Reintento en {DelaySeconds}s. Pendientes={Pending}",
                    pending.BatchId,
                    (int)delay.TotalSeconds,
                    _buffer.Count);
                return delay;
            }
        }

        if (sentCount > 0)
            _logger.LogInformation("Ingest OK. Batches enviados={SentCount}. Pendientes={Pending}", sentCount, _buffer.Count);

        return null;
    }

    private MetricsIngestRequest BuildIngestRequest(DateTime nowUtc)
    {
        var metrics = _collector.Collect(nowUtc);

        return new MetricsIngestRequest
        {
            HostId = _options.HostId,
            TimestampUtc = nowUtc,
            Metrics = metrics
        };
    }

    private TimeSpan ComputeBackoff(int initialSeconds, int maxSeconds, double multiplier, int attempt, double jitterRatio)
    {
        var cappedAttempt = Math.Max(1, attempt);
        var raw = initialSeconds * Math.Pow(multiplier, cappedAttempt - 1);
        var bounded = Math.Min(maxSeconds, raw);
        var jitterFactor = 1 + ((_random.NextDouble() * 2 - 1) * jitterRatio);
        var seconds = Math.Max(1, bounded * jitterFactor);
        return TimeSpan.FromSeconds(seconds);
    }
}
