using System.Text.Json;

namespace CentralMonitoring.Agent;

public class LocalBatchBuffer
{
    private readonly object _sync = new();
    private readonly ILogger<LocalBatchBuffer> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private Queue<PendingIngestBatch> _queue = new();
    private string _filePath = "buffer/agent-buffer.json";
    private int _maxBatches = 500;

    public LocalBatchBuffer(ILogger<LocalBatchBuffer> logger)
    {
        _logger = logger;
    }

    public int Count
    {
        get
        {
            lock (_sync) return _queue.Count;
        }
    }

    public void Initialize(string filePath, int maxBatches)
    {
        lock (_sync)
        {
            _filePath = Path.GetFullPath(filePath);
            _maxBatches = maxBatches;
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            if (!File.Exists(_filePath))
            {
                _queue = new Queue<PendingIngestBatch>();
                return;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var items = JsonSerializer.Deserialize<List<PendingIngestBatch>>(json, _jsonOptions) ?? new List<PendingIngestBatch>();
                _queue = new Queue<PendingIngestBatch>(items.OrderBy(x => x.EnqueuedAtUtc));
                if (_queue.Count > _maxBatches)
                {
                    while (_queue.Count > _maxBatches) _queue.Dequeue();
                }

                _logger.LogInformation("Buffer local cargado. Archivo={FilePath} Batches={Count}", _filePath, _queue.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer buffer local, se iniciara vacio. Archivo={FilePath}", _filePath);
                _queue = new Queue<PendingIngestBatch>();
            }
        }
    }

    public bool TryEnqueue(PendingIngestBatch batch, out bool droppedOldest)
    {
        lock (_sync)
        {
            droppedOldest = false;
            if (_queue.Count >= _maxBatches)
            {
                _queue.Dequeue();
                droppedOldest = true;
            }

            _queue.Enqueue(batch);
            PersistUnsafe();
            return true;
        }
    }

    public bool TryPeek(out PendingIngestBatch? batch)
    {
        lock (_sync)
        {
            if (_queue.Count == 0)
            {
                batch = null;
                return false;
            }

            batch = _queue.Peek();
            return true;
        }
    }

    public void UpdateHead(PendingIngestBatch batch)
    {
        lock (_sync)
        {
            if (_queue.Count == 0) return;

            _queue.Dequeue();
            var items = new List<PendingIngestBatch> { batch };
            items.AddRange(_queue);
            _queue = new Queue<PendingIngestBatch>(items);
            PersistUnsafe();
        }
    }

    public bool TryDequeue(out PendingIngestBatch? removed)
    {
        lock (_sync)
        {
            if (_queue.Count == 0)
            {
                removed = null;
                return false;
            }

            removed = _queue.Dequeue();
            PersistUnsafe();
            return true;
        }
    }

    private void PersistUnsafe()
    {
        var snapshot = _queue.ToList();
        var tempPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, true);
    }
}
