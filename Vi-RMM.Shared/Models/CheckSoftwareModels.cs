namespace ViRMM.Shared.Models;

public class SystemCheckDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Category { get; set; } = "General"; // Security, Performance, Storage, System
    public bool IsPassing { get; set; } = true;
    public string StatusText { get; set; } = "Healthy";
    public string Details { get; set; } = "";
    public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
}

public class InstalledSoftwareDto
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string InstallDate { get; set; } = "";
    public string SizeFormatted { get; set; } = "";
}

public class WindowsPatchDto
{
    public string KbArticleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Severity { get; set; } = "Important";
    public string Status { get; set; } = "Installed"; // Installed, Missing, Pending Reboot
    public DateTime? InstalledOnUtc { get; set; }
}
