namespace ViRMM.Shared.Models;

public class CommandRequestDto
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    public string AgentId { get; set; } = "";
    public string Shell { get; set; } = "PowerShell"; // "PowerShell", "CMD"
    public string Command { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
}

public class CommandOutputChunkDto
{
    public string ExecutionId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsError { get; set; }
    public bool IsCompleted { get; set; }
    public int? ExitCode { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
