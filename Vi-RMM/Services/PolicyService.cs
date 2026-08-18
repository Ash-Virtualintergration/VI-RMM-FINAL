namespace ViRMM.Services;

public class MonitoringPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default Endpoint Policy";
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int OfflineThresholdMinutes { get; set; } = 3;
    public int HighCpuThresholdPercent { get; set; } = 85;
    public int HighCpuDurationMinutes { get; set; } = 10;
    public int LowDiskThresholdPercent { get; set; } = 15;
    public bool AutoCheckDefender { get; set; } = true;
    public bool AutoCheckFirewall { get; set; } = true;
    public bool EmailAlertsOnFailure { get; set; } = true;
    public string TargetGroup { get; set; } = "All Workstations & Servers";
}

public class PolicyService
{
    private readonly List<MonitoringPolicy> _policies =
    [
        new() { Name = "Standard Workstation Policy", TargetGroup = "Workstations", HighCpuThresholdPercent = 90, LowDiskThresholdPercent = 10 },
        new() { Name = "Mission-Critical Server Policy", TargetGroup = "Servers", HighCpuThresholdPercent = 80, LowDiskThresholdPercent = 20, OfflineThresholdMinutes = 1 }
    ];

    public IReadOnlyList<MonitoringPolicy> All => _policies;

    public void Save(MonitoringPolicy policy)
    {
        var existing = _policies.FirstOrDefault(p => p.Id == policy.Id);
        if (existing != null)
        {
            existing.Name = policy.Name;
            existing.HeartbeatIntervalSeconds = policy.HeartbeatIntervalSeconds;
            existing.OfflineThresholdMinutes = policy.OfflineThresholdMinutes;
            existing.HighCpuThresholdPercent = policy.HighCpuThresholdPercent;
            existing.LowDiskThresholdPercent = policy.LowDiskThresholdPercent;
            existing.AutoCheckDefender = policy.AutoCheckDefender;
            existing.AutoCheckFirewall = policy.AutoCheckFirewall;
            existing.EmailAlertsOnFailure = policy.EmailAlertsOnFailure;
            existing.TargetGroup = policy.TargetGroup;
        }
        else
        {
            _policies.Add(policy);
        }
    }

    public void Delete(Guid id) => _policies.RemoveAll(p => p.Id == id);
}
