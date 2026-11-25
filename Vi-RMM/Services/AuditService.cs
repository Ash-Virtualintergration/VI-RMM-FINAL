namespace ViRMM.Services;

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string User { get; set; } = "";
    public string TargetAgent { get; set; } = "";
    public string Action { get; set; } = "";
    public string Category { get; set; } = "Command"; // Command, Remote, File, Service, Auth, Policy
    public string Details { get; set; } = "";
    public string Status { get; set; } = "Success"; // Success, Failed, Warning
}

public class AuditService
{
    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> All => _entries.OrderByDescending(e => e.TimestampUtc).ToList();

    public void Log(string user, string targetAgent, string action, string category, string details, string status = "Success")
    {
        _entries.Insert(0, new AuditEntry
        {
            User = user,
            TargetAgent = targetAgent,
            Action = action,
            Category = category,
            Details = details,
            Status = status,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
