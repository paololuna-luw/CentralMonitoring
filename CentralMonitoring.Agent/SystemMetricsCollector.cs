using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text.Json;
using CentralMonitoring.Shared.DTOs.Metrics;

namespace CentralMonitoring.Agent;

public class SystemMetricsCollector
{
    private readonly AgentOptions _options;
    private readonly int _processorCount = Math.Max(1, Environment.ProcessorCount);

    private DateTime _lastProcessCpuSampleUtc = DateTime.UtcNow;
    private TimeSpan _lastProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

    private readonly Dictionary<string, NetworkSample> _networkSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DiskIoSample> _diskIoSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ProcessCpuSample> _processCpuSnapshots = new();

    private CpuTotalSample? _lastCpuTotalSample;

    public SystemMetricsCollector(AgentOptions options)
    {
        _options = options;
    }

    public List<MetricPointDto> Collect(DateTime nowUtc)
    {
        var metrics = new List<MetricPointDto>
        {
            Build("agent_heartbeat", 1),
            Build("uptime_seconds", Environment.TickCount64 / 1000.0)
        };

        CollectProcessCpu(metrics, nowUtc);
        CollectSystemCpu(metrics, nowUtc);
        CollectLinuxLoadAverage(metrics);
        CollectMemory(metrics);
        CollectDisks(metrics);
        CollectDiskIo(metrics, nowUtc);
        CollectNetwork(metrics, nowUtc);
        CollectTopProcesses(metrics, nowUtc);
        CollectCriticalServices(metrics);

        return metrics;
    }

    private void CollectProcessCpu(List<MetricPointDto> metrics, DateTime nowUtc)
    {
        var currentCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        var elapsedMs = (nowUtc - _lastProcessCpuSampleUtc).TotalMilliseconds;
        if (elapsedMs <= 0)
            return;

        var cpuDeltaMs = (currentCpuTime - _lastProcessCpuTime).TotalMilliseconds;
        var cpuPercent = (cpuDeltaMs / (elapsedMs * _processorCount)) * 100.0;
        cpuPercent = Math.Clamp(cpuPercent, 0, 100);

        metrics.Add(Build("agent_cpu_usage_pct", cpuPercent));

        _lastProcessCpuSampleUtc = nowUtc;
        _lastProcessCpuTime = currentCpuTime;
    }

    private void CollectSystemCpu(List<MetricPointDto> metrics, DateTime nowUtc)
    {
        var sample = CpuTotalReader.TryRead(nowUtc);
        if (sample is null)
            return;

        if (_lastCpuTotalSample is not null)
        {
            var deltaTotal = sample.TotalTicks - _lastCpuTotalSample.TotalTicks;
            var deltaIdle = sample.IdleTicks - _lastCpuTotalSample.IdleTicks;
            if (deltaTotal > 0)
            {
                var usage = ((double)(deltaTotal - deltaIdle) / deltaTotal) * 100.0;
                metrics.Add(Build("cpu_usage_pct", Math.Clamp(usage, 0, 100)));
            }
        }

        _lastCpuTotalSample = sample;
    }

    private void CollectLinuxLoadAverage(List<MetricPointDto> metrics)
    {
        if (!OperatingSystem.IsLinux()) return;

        var load = LinuxLoadAverageReader.TryRead();
        if (load is null) return;

        metrics.Add(Build("load1", load.Load1));
        metrics.Add(Build("load5", load.Load5));
        metrics.Add(Build("load15", load.Load15));
    }

    private void CollectMemory(List<MetricPointDto> metrics)
    {
        var mem = MemoryInfoReader.TryRead();
        if (mem is null || mem.TotalBytes <= 0)
            return;

        var usedBytes = Math.Max(0, mem.TotalBytes - mem.AvailableBytes);
        var usedPct = (double)usedBytes / mem.TotalBytes * 100.0;

        metrics.Add(Build("mem_total_mb", BytesToMb(mem.TotalBytes)));
        metrics.Add(Build("mem_available_mb", BytesToMb(mem.AvailableBytes)));
        metrics.Add(Build("mem_used_mb", BytesToMb(usedBytes)));
        metrics.Add(Build("mem_used_pct", usedPct));
    }

    private void CollectDisks(List<MetricPointDto> metrics)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Network) continue;

            var total = drive.TotalSize;
            if (total <= 0) continue;

            var free = drive.AvailableFreeSpace;
            var used = total - free;
            var usedPct = (double)used / total * 100.0;

            var labels = new
            {
                drive = drive.Name,
                fs = Safe(drive.DriveFormat)
            };

            metrics.Add(Build("disk_total_mb", BytesToMb(total), labels));
            metrics.Add(Build("disk_free_mb", BytesToMb(free), labels));
            metrics.Add(Build("disk_used_mb", BytesToMb(used), labels));
            metrics.Add(Build("disk_used_pct", usedPct, labels));
        }
    }

    private void CollectDiskIo(List<MetricPointDto> metrics, DateTime nowUtc)
    {
        foreach (var current in DiskIoReader.Read(nowUtc))
        {
            var labels = new { disk = current.DiskName };

            if (_diskIoSnapshots.TryGetValue(current.DiskName, out var prev))
            {
                var elapsed = (current.TimestampUtc - prev.TimestampUtc).TotalSeconds;
                if (elapsed > 0)
                {
                    var readBytesPerSec = Math.Max(0, (current.ReadSectors - prev.ReadSectors) * 512d / elapsed);
                    var writeBytesPerSec = Math.Max(0, (current.WriteSectors - prev.WriteSectors) * 512d / elapsed);
                    metrics.Add(Build("disk_read_bytes_per_sec", readBytesPerSec, labels));
                    metrics.Add(Build("disk_write_bytes_per_sec", writeBytesPerSec, labels));
                }
            }

            metrics.Add(Build("disk_queue_len", current.InFlightIo, labels));
            _diskIoSnapshots[current.DiskName] = current;
        }
    }

    private void CollectNetwork(List<MetricPointDto> metrics, DateTime nowUtc)
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        foreach (var nic in interfaces)
        {
            var stats = nic.GetIPv4Statistics();
            var rxDrops = 0L;
            var txDrops = 0L;
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                rxDrops = stats.IncomingPacketsDiscarded;
                txDrops = stats.OutgoingPacketsDiscarded;
            }

            var current = new NetworkSample
            {
                TimestampUtc = nowUtc,
                RxBytes = stats.BytesReceived,
                TxBytes = stats.BytesSent,
                RxErrors = stats.IncomingPacketsWithErrors,
                TxErrors = stats.OutgoingPacketsWithErrors,
                RxDrops = rxDrops,
                TxDrops = txDrops
            };

            var labels = new
            {
                iface = Safe(nic.Name)
            };

            metrics.Add(Build("net_rx_bytes_total", current.RxBytes, labels));
            metrics.Add(Build("net_tx_bytes_total", current.TxBytes, labels));
            metrics.Add(Build("net_rx_errors", current.RxErrors, labels));
            metrics.Add(Build("net_tx_errors", current.TxErrors, labels));
            metrics.Add(Build("net_rx_drops", current.RxDrops, labels));
            metrics.Add(Build("net_tx_drops", current.TxDrops, labels));

            if (_networkSnapshots.TryGetValue(nic.Id, out var previous))
            {
                var elapsed = (current.TimestampUtc - previous.TimestampUtc).TotalSeconds;
                if (elapsed > 0)
                {
                    var rxRate = Math.Max(0, (current.RxBytes - previous.RxBytes) / elapsed);
                    var txRate = Math.Max(0, (current.TxBytes - previous.TxBytes) / elapsed);
                    metrics.Add(Build("net_rx_bytes_per_sec", rxRate, labels));
                    metrics.Add(Build("net_tx_bytes_per_sec", txRate, labels));
                }
            }

            _networkSnapshots[nic.Id] = current;
        }
    }

    private void CollectTopProcesses(List<MetricPointDto> metrics, DateTime nowUtc)
    {
        var processPoints = new List<ProcessPoint>();
        var seenPids = new HashSet<int>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                using (p)
                {
                    var pid = p.Id;
                    seenPids.Add(pid);

                    var cpuTime = p.TotalProcessorTime;
                    var memMb = BytesToMb(p.WorkingSet64);
                    var name = Safe(p.ProcessName);

                    double cpuPct = 0;
                    if (_processCpuSnapshots.TryGetValue(pid, out var prev))
                    {
                        var elapsedMs = (nowUtc - prev.TimestampUtc).TotalMilliseconds;
                        if (elapsedMs > 0)
                        {
                            var cpuDeltaMs = (cpuTime - prev.CpuTime).TotalMilliseconds;
                            cpuPct = Math.Clamp((cpuDeltaMs / (elapsedMs * _processorCount)) * 100.0, 0, 100);
                        }
                    }

                    _processCpuSnapshots[pid] = new ProcessCpuSample
                    {
                        CpuTime = cpuTime,
                        TimestampUtc = nowUtc,
                        ProcessName = name
                    };

                    processPoints.Add(new ProcessPoint
                    {
                        Pid = pid,
                        ProcessName = name,
                        CpuPct = cpuPct,
                        MemMb = memMb
                    });
                }
            }
            catch
            {
                // process may exit or deny access
            }
        }

        var stalePids = _processCpuSnapshots.Keys.Where(pid => !seenPids.Contains(pid)).ToList();
        foreach (var pid in stalePids)
            _processCpuSnapshots.Remove(pid);

        var topCpu = processPoints
            .OrderByDescending(x => x.CpuPct)
            .Take(_options.TopProcessesCount)
            .ToList();

        foreach (var p in topCpu)
        {
            metrics.Add(Build("proc_cpu_pct", p.CpuPct, new { process = p.ProcessName, pid = p.Pid }));
        }

        var topMem = processPoints
            .OrderByDescending(x => x.MemMb)
            .Take(_options.TopProcessesCount)
            .ToList();

        foreach (var p in topMem)
        {
            metrics.Add(Build("proc_mem_mb", p.MemMb, new { process = p.ProcessName, pid = p.Pid }));
        }
    }

    private void CollectCriticalServices(List<MetricPointDto> metrics)
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var serviceName in _options.CriticalWindowsServices)
            {
                if (string.IsNullOrWhiteSpace(serviceName)) continue;
                var up = WindowsServiceReader.IsRunning(serviceName.Trim()) ? 1 : 0;
                metrics.Add(Build("service_up", up, new { service = serviceName.Trim(), kind = "windows_service" }));
            }
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            foreach (var unit in _options.CriticalLinuxSystemdUnits)
            {
                if (string.IsNullOrWhiteSpace(unit)) continue;
                var up = SystemdReader.IsActive(unit.Trim()) ? 1 : 0;
                metrics.Add(Build("service_up", up, new { service = unit.Trim(), kind = "systemd" }));
            }
        }
    }

    private static MetricPointDto Build(string key, double value, object? labels = null)
    {
        return new MetricPointDto
        {
            Key = key,
            Value = value,
            LabelsJson = labels is null ? "{\"agent\":\"program2\"}" : JsonSerializer.Serialize(labels)
        };
    }

    private static double BytesToMb(long bytes) => bytes / 1024d / 1024d;

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private sealed class NetworkSample
    {
        public DateTime TimestampUtc { get; set; }
        public long RxBytes { get; set; }
        public long TxBytes { get; set; }
        public long RxErrors { get; set; }
        public long TxErrors { get; set; }
        public long RxDrops { get; set; }
        public long TxDrops { get; set; }
    }

    private sealed class ProcessCpuSample
    {
        public TimeSpan CpuTime { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string ProcessName { get; set; } = "unknown";
    }

    private sealed class ProcessPoint
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; } = "unknown";
        public double CpuPct { get; set; }
        public double MemMb { get; set; }
    }
}

internal sealed class MemorySnapshot
{
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
}

internal static class MemoryInfoReader
{
    public static MemorySnapshot? TryRead()
    {
        if (OperatingSystem.IsWindows())
            return TryReadWindows();

        if (OperatingSystem.IsLinux())
            return TryReadLinux();

        return null;
    }

    private static MemorySnapshot? TryReadWindows()
    {
        var status = new MEMORYSTATUSEX();
        status.dwLength = (uint)Marshal.SizeOf(status);

        if (!GlobalMemoryStatusEx(ref status))
            return null;

        return new MemorySnapshot
        {
            TotalBytes = (long)status.ullTotalPhys,
            AvailableBytes = (long)status.ullAvailPhys
        };
    }

    private static MemorySnapshot? TryReadLinux()
    {
        const string memInfoPath = "/proc/meminfo";
        if (!File.Exists(memInfoPath))
            return null;

        long totalKb = 0;
        long availableKb = 0;

        foreach (var line in File.ReadLines(memInfoPath))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                totalKb = ParseKb(line);
                continue;
            }

            if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                availableKb = ParseKb(line);
            }
        }

        if (totalKb <= 0 || availableKb <= 0)
            return null;

        return new MemorySnapshot
        {
            TotalBytes = totalKb * 1024,
            AvailableBytes = availableKb * 1024
        };
    }

    private static long ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return 0;
        return long.TryParse(parts[1], out var value) ? value : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}

internal sealed class CpuTotalSample
{
    public DateTime TimestampUtc { get; set; }
    public ulong TotalTicks { get; set; }
    public ulong IdleTicks { get; set; }
}

internal static class CpuTotalReader
{
    public static CpuTotalSample? TryRead(DateTime nowUtc)
    {
        if (OperatingSystem.IsLinux())
            return TryReadLinux(nowUtc);

        if (OperatingSystem.IsWindows())
            return TryReadWindows(nowUtc);

        return null;
    }

    private static CpuTotalSample? TryReadLinux(DateTime nowUtc)
    {
        const string path = "/proc/stat";
        if (!File.Exists(path)) return null;

        var cpuLine = File.ReadLines(path).FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
        if (cpuLine is null) return null;

        var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return null;

        ulong total = 0;
        for (var i = 1; i < parts.Length; i++)
        {
            if (ulong.TryParse(parts[i], out var n)) total += n;
        }

        var idle = ulong.TryParse(parts[4], out var idleVal) ? idleVal : 0;
        return new CpuTotalSample { TimestampUtc = nowUtc, TotalTicks = total, IdleTicks = idle };
    }

    private static CpuTotalSample? TryReadWindows(DateTime nowUtc)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return null;

        var idleTicks = ToUInt64(idle);
        var kernelTicks = ToUInt64(kernel);
        var userTicks = ToUInt64(user);
        var totalTicks = kernelTicks + userTicks;

        return new CpuTotalSample
        {
            TimestampUtc = nowUtc,
            TotalTicks = totalTicks,
            IdleTicks = idleTicks
        };
    }

    private static ulong ToUInt64(FILETIME time) => ((ulong)time.dwHighDateTime << 32) | time.dwLowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }
}

internal sealed class LinuxLoadAverage
{
    public double Load1 { get; set; }
    public double Load5 { get; set; }
    public double Load15 { get; set; }
}

internal static class LinuxLoadAverageReader
{
    public static LinuxLoadAverage? TryRead()
    {
        const string path = "/proc/loadavg";
        if (!File.Exists(path)) return null;

        var line = File.ReadAllText(path).Trim();
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return null;

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var load1)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var load5)) return null;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var load15)) return null;

        return new LinuxLoadAverage { Load1 = load1, Load5 = load5, Load15 = load15 };
    }
}

internal sealed class DiskIoSample
{
    public string DiskName { get; set; } = "unknown";
    public DateTime TimestampUtc { get; set; }
    public long ReadSectors { get; set; }
    public long WriteSectors { get; set; }
    public long InFlightIo { get; set; }
}

internal static class DiskIoReader
{
    public static IEnumerable<DiskIoSample> Read(DateTime nowUtc)
    {
        if (!OperatingSystem.IsLinux()) yield break;

        const string path = "/proc/diskstats";
        if (!File.Exists(path)) yield break;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 14) continue;

            var disk = parts[2];
            if (!IsPhysicalDisk(disk)) continue;

            if (!long.TryParse(parts[5], out var readSectors)) continue;
            if (!long.TryParse(parts[9], out var writeSectors)) continue;
            _ = long.TryParse(parts[11], out var inFlight);

            yield return new DiskIoSample
            {
                DiskName = disk,
                TimestampUtc = nowUtc,
                ReadSectors = readSectors,
                WriteSectors = writeSectors,
                InFlightIo = inFlight
            };
        }
    }

    private static bool IsPhysicalDisk(string disk)
    {
        if (disk.StartsWith("loop", StringComparison.OrdinalIgnoreCase)) return false;
        if (disk.StartsWith("ram", StringComparison.OrdinalIgnoreCase)) return false;
        if (disk.StartsWith("dm-", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

internal static class WindowsServiceReader
{
    [SupportedOSPlatform("windows")]
    public static bool IsRunning(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }
}

internal static class SystemdReader
{
    public static bool IsActive(string unit)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"is-active {Escape(unit)}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) return false;

            if (!p.WaitForExit(3000))
            {
                try { p.Kill(true); } catch { }
                return false;
            }

            var output = p.StandardOutput.ReadToEnd().Trim();
            return p.ExitCode == 0 && string.Equals(output, "active", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\\\"");
    }
}
