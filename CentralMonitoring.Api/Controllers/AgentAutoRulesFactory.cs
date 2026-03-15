using CentralMonitoring.Domain.Entities;

namespace CentralMonitoring.Api.Controllers;

public static class AgentAutoRulesFactory
{
    public static List<Rule> Build(IConfiguration config, Guid hostId, string? hostType)
    {
        var enabled = config.GetValue<bool?>("Agent:AutoRules:Enabled") ?? true;
        if (!enabled) return new List<Rule>();

        var now = DateTime.UtcNow;
        var patterns = config.GetSection("Agent:AutoRules:Patterns").Get<List<AgentAutoRulePattern>>() ?? DefaultPatterns();
        if (patterns.Count == 0) patterns = DefaultPatterns();

        var rules = new List<Rule>();
        foreach (var p in patterns.Where(x => x.Enabled))
        {
            if (string.IsNullOrWhiteSpace(p.MetricKey) || string.IsNullOrWhiteSpace(p.Operator) || string.IsNullOrWhiteSpace(p.Severity))
                continue;

            rules.Add(new Rule
            {
                MetricKey = p.MetricKey.Trim(),
                Operator = p.Operator.Trim(),
                Threshold = p.Threshold,
                WindowMinutes = p.WindowMinutes <= 0 ? 2 : p.WindowMinutes,
                Severity = p.Severity.Trim(),
                HostId = hostId,
                LabelContains = string.IsNullOrWhiteSpace(p.LabelContains) ? null : p.LabelContains.Trim(),
                Enabled = p.Enabled,
                CreatedAtUtc = now
            });
        }

        var normalizedHostType = NormalizeHostType(hostType);
        var serviceNames = normalizedHostType switch
        {
            "windows" => config.GetSection("Agent:AutoRules:CriticalWindowsServices").Get<List<string>>() ?? new List<string>(),
            "linux" => config.GetSection("Agent:AutoRules:CriticalLinuxSystemdUnits").Get<List<string>>() ?? new List<string>(),
            _ => new List<string>()
        };
        var serviceWindow = config.GetValue<int?>("Agent:AutoRules:ServiceRule:WindowMinutes") ?? 2;
        var serviceSeverity = config.GetValue<string>("Agent:AutoRules:ServiceRule:Severity") ?? "Critical";
        var serviceEnabled = config.GetValue<bool?>("Agent:AutoRules:ServiceRule:Enabled") ?? true;

        if (serviceEnabled)
        {
            foreach (var serviceName in serviceNames.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                rules.Add(new Rule
                {
                    MetricKey = "service_up",
                    Operator = "==",
                    Threshold = 0,
                    WindowMinutes = Math.Max(1, serviceWindow),
                    Severity = serviceSeverity,
                    HostId = hostId,
                    LabelContains = $"\"service\":\"{serviceName}\"",
                    Enabled = true,
                    CreatedAtUtc = now
                });
            }
        }

        return rules;
    }

    private static string NormalizeHostType(string? hostType)
    {
        if (string.IsNullOrWhiteSpace(hostType)) return "";
        var t = hostType.Trim().ToLowerInvariant();
        if (t.Contains("windows")) return "windows";
        if (t.Contains("linux")) return "linux";
        return t;
    }

    private static List<AgentAutoRulePattern> DefaultPatterns() => new()
    {
        new AgentAutoRulePattern { MetricKey = "cpu_usage_pct", Operator = ">", Threshold = 85, WindowMinutes = 3, Severity = "Critical", Enabled = true },
        new AgentAutoRulePattern { MetricKey = "mem_used_pct", Operator = ">", Threshold = 90, WindowMinutes = 3, Severity = "Critical", Enabled = true },
        new AgentAutoRulePattern { MetricKey = "disk_used_pct", Operator = ">", Threshold = 92, WindowMinutes = 5, Severity = "Critical", Enabled = true },
        new AgentAutoRulePattern { MetricKey = "net_rx_errors", Operator = ">", Threshold = 0, WindowMinutes = 5, Severity = "Warning", Enabled = true },
        new AgentAutoRulePattern { MetricKey = "net_tx_errors", Operator = ">", Threshold = 0, WindowMinutes = 5, Severity = "Warning", Enabled = true }
    };
}

public class AgentAutoRulePattern
{
    public string MetricKey { get; set; } = "";
    public string Operator { get; set; } = ">";
    public double Threshold { get; set; }
    public int WindowMinutes { get; set; } = 2;
    public string Severity { get; set; } = "Warning";
    public string? LabelContains { get; set; }
    public bool Enabled { get; set; } = true;
}
