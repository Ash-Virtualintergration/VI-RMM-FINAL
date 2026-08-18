namespace ViRMM.Services;

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string User { get; set; } = "admin";
    public string TargetAgent { get; set; } = "";
    public string Action { get; set; } = "";
    public string Category { get; set; } = "Command"; // Command, Remote, File, Service, Auth, Policy
    public string Details { get; set; } = "";
    public string Status { get; set; } = "Success"; // Success, Failed, Warning
}

public class AuditService
{
    private readonly List<AuditEntry> _entries =
    [
        new() { User = "admin", TargetAgent = "PC-CPT-001", Action = "Execute Command", Category = "Command", Details = "ipconfig /all", Status = "Success", TimestampUtc = DateTime.UtcNow.AddMinutes(-12) },
        new() { User = "admin", TargetAgent = "PC-CPT-001", Action = "Remote Desktop Session", Category = "Remote", Details = "Session opened (15m duration)", Status = "Success", TimestampUtc = DateTime.UtcNow.AddMinutes(-30) },
        new() { User = "tech", TargetAgent = "SRV-LON-001", Action = "Service Restart", Category = "Service", Details = "Restarted Spooler service", Status = "Success", TimestampUtc = DateTime.UtcNow.AddHours(-2) },
        new() { User = "admin", TargetAgent = "PC-NY-014", Action = "File Download", Category = "File", Details = "Downloaded C:\\logs\\app.log", Status = "Success", TimestampUtc = DateTime.UtcNow.AddHours(-4) },
        new() { User = "admin", TargetAgent = "System", Action = "User Login", Category = "Auth", Details = "Successful login from 192.168.1.50", Status = "Success", TimestampUtc = DateTime.UtcNow.AddHours(-6) }
    ];

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
