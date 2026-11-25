namespace ViRMM.Shared.Models;

public class AgentRegistrationDto
{
    public string AgentId { get; set; } = Guid.NewGuid().ToString("N");
    public string Hostname { get; set; } = Environment.MachineName;
    public string ClientName { get; set; } = "";
    public string SiteName { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string ProcessorName { get; set; } = "";
    public int CoreCount { get; set; } = Environment.ProcessorCount;
    public long TotalMemoryBytes { get; set; }
    public long TotalDiskBytes { get; set; }
    public long FreeDiskBytes { get; set; }
    public string LoggedUser { get; set; } = Environment.UserName;
    public string PublicIp { get; set; } = "";
    public string LocalIp { get; set; } = "";
    public string AgentVersion { get; set; } = "1.0.0";
    public List<DriveItemDto> Drives { get; set; } = [];
    public List<NetworkAdapterDto> NetworkAdapters { get; set; } = [];
    public bool IsLiveAgent { get; set; } = true;
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
}

public class AgentHeartbeatDto
{
    public string AgentId { get; set; } = "";
    public double CpuUsagePercent { get; set; }
    public long UsedMemoryBytes { get; set; }
    public long TotalMemoryBytes { get; set; }
    public long FreeMemoryBytes { get; set; }
    public long FreeDiskBytes { get; set; }
    public long TotalDiskBytes { get; set; }
    public string LoggedUser { get; set; } = "";
    public TimeSpan UpTime { get; set; }
    public int ProcessCount { get; set; }
    public int ServiceCount { get; set; }
    public int HealthyChecksCount { get; set; }
    public int TotalChecksCount { get; set; }
    public string Status { get; set; } = "Online";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class NetworkAdapterDto
{
    public string Name { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsUp { get; set; }
}

public class DriveItemDto
{
    public string Name { get; set; } = "";
    public string VolumeLabel { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public double UsedPercent => TotalBytes > 0 ? Math.Round((double)(TotalBytes - FreeBytes) / TotalBytes * 100, 1) : 0;
}
