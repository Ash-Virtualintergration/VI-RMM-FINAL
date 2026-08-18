namespace ViRMM.Shared.Models;

public class ProcessDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public long WorkingSetBytes { get; set; }
    public double WorkingSetMb => Math.Round((double)WorkingSetBytes / (1024 * 1024), 1);
    public double CpuPercent { get; set; }
    public int ThreadCount { get; set; }
    public string MainWindowTitle { get; set; } = "";
    public string UserName { get; set; } = "";
    public bool Responding { get; set; } = true;
}

public class ServiceItemDto
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "Stopped"; // Running, Stopped, Paused
    public string StartType { get; set; } = "Automatic"; // Automatic, Manual, Disabled
    public bool CanStop { get; set; }
    public bool CanPauseAndContinue { get; set; }
}
