namespace ViRMM.Services;

public class RmmScript
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Maintenance"; // Maintenance, Network, Diagnostics, Security
    public string Shell { get; set; } = "PowerShell";
    public string ScriptBody { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ScriptService
{
    private readonly List<RmmScript> _scripts =
    [
        new()
        {
            Name = "Flush DNS & Reset IP Stack",
            Description = "Clears DNS cache and releases/renews DHCP lease for network troubleshooting.",
            Category = "Network",
            Shell = "PowerShell",
            ScriptBody = "ipconfig /flushdns\r\nipconfig /release\r\nipconfig /renew\r\nGet-NetIPAddress | Format-Table -AutoSize"
        },
        new()
        {
            Name = "System Health & Disk Cleanup",
            Description = "Purges temporary directories and checks free disk capacity across all volumes.",
            Category = "Maintenance",
            Shell = "PowerShell",
            ScriptBody = "Get-PSDrive -PSProvider FileSystem | Select-Object Name, @{N='FreeGB';E={[math]::Round($_.Free/1GB,2)}}, @{N='UsedGB';E={[math]::Round($_.Used/1GB,2)}}\r\nClear-RecycleBin -Force -ErrorAction SilentlyContinue\r\nWrite-Output 'Disk health scan completed.'"
        },
        new()
        {
            Name = "Restart Windows Print Spooler",
            Description = "Stops the Spooler service, purges stalled printer queue jobs, and restarts the service.",
            Category = "Maintenance",
            Shell = "PowerShell",
            ScriptBody = "Stop-Service -Name Spooler -Force\r\nRemove-Item -Path \"$env:SystemRoot\\System32\\spool\\PRINTERS\\*\" -Force -Recurse -ErrorAction SilentlyContinue\r\nStart-Service -Name Spooler\r\nGet-Service -Name Spooler"
        },
        new()
        {
            Name = "Windows Security & Firewall Audit",
            Description = "Audits Windows Defender status, firewall profiles, and recent failed logon events.",
            Category = "Security",
            Shell = "PowerShell",
            ScriptBody = "Get-MpComputerStatus | Select-Object AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureAge\r\nGet-NetFirewallProfile | Select-Object Name, Enabled\r\nGet-WinEvent -FilterHashtable @{LogName='Security';Id=4625} -MaxEvents 5 -ErrorAction SilentlyContinue | Select-Object TimeCreated, Message"
        },
        new()
        {
            Name = "List Top CPU & RAM Consumers",
            Description = "Gathers the top 10 memory and CPU intensive processes running on the machine.",
            Category = "Diagnostics",
            Shell = "PowerShell",
            ScriptBody = "Get-Process | Sort-Object -Descending WorkingSet64 | Select-Object -First 10 Id, ProcessName, @{N='MemoryMB';E={[math]::Round($_.WorkingSet64/1MB,1)}}, CPU"
        }
    ];

    public IReadOnlyList<RmmScript> All => _scripts;

    public void Add(RmmScript script) => _scripts.Add(script);
    public void Delete(Guid id) => _scripts.RemoveAll(s => s.Id == id);
}
