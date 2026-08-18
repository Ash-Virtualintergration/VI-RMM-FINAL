using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ViRMM.Hubs;
using ViRMM.Shared.Models;

namespace ViRMM.Services;

public class ConnectedAgent
{
    public string AgentId { get; set; } = "";
    public string? ConnectionId { get; set; }
    public AgentRegistrationDto Registration { get; set; } = new();
    public AgentHeartbeatDto? LatestHeartbeat { get; set; }
    public bool IsLiveConnected => !string.IsNullOrEmpty(ConnectionId);
    public string Status => IsLiveConnected ? (LatestHeartbeat?.Status ?? "Online") : "Offline";
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public string Client => Registration.ClientName;
    public string Site => Registration.SiteName;
    public string Hostname => Registration.Hostname;
    public string User => LatestHeartbeat?.LoggedUser ?? Registration.LoggedUser;
    public string OperatingSystem => Registration.OperatingSystem;
    public double CpuPercent => LatestHeartbeat?.CpuUsagePercent ?? 12.5;
    public double MemoryUsedPercent
    {
        get
        {
            if (Registration.TotalMemoryBytes <= 0) return 45.0;
            var used = LatestHeartbeat?.UsedMemoryBytes ?? (Registration.TotalMemoryBytes / 2);
            return Math.Round((double)used / Registration.TotalMemoryBytes * 100, 1);
        }
    }
    public string LastSeenFormatted
    {
        get
        {
            if (IsLiveConnected) return "Live";
            var span = DateTime.UtcNow - LastSeenUtc;
            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }

    public List<SystemCheckDto> Checks { get; set; } = [];
    public List<ProcessDto> Processes { get; set; } = [];
    public List<ServiceItemDto> Services { get; set; } = [];
    public List<InstalledSoftwareDto> Software { get; set; } = [];
    public List<WindowsPatchDto> Patches { get; set; } = [];
    public DirectoryListingDto? CurrentDirectory { get; set; }
    public ScreenFrameDto? LatestScreen { get; set; }
    public string Notes { get; set; } = "";
}

public class AgentService
{
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly ILogger<AgentService> _logger;
    private readonly ConcurrentDictionary<string, ConnectedAgent> _agents = new();
    private readonly ConcurrentDictionary<string, string> _connectionToAgentId = new();

    public event Action? OnChange;
    public event Action<CommandOutputChunkDto>? OnCommandOutput;
    public event Action<ScreenFrameDto>? OnScreenFrame;
    public event Action<DirectoryListingDto>? OnDirectoryListing;
    public event Action<FileContentResponseDto>? OnFileContent;
    public event Action<FileOperationResultDto>? OnFileOpResult;
    public event Action<string, List<ProcessDto>>? OnProcessesUpdated;
    public event Action<string, List<ServiceItemDto>>? OnServicesUpdated;
    public event Action<string, List<SystemCheckDto>>? OnChecksUpdated;
    public event Action<string, List<InstalledSoftwareDto>>? OnSoftwareUpdated;

    public AgentService(IHubContext<AgentHub> hubContext, ILogger<AgentService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        SeedDefaultAgents();
    }

    public IReadOnlyList<ConnectedAgent> GetAllAgents() => _agents.Values.OrderByDescending(a => a.IsLiveConnected).ThenBy(a => a.Hostname).ToList();

    public ConnectedAgent? GetAgent(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return agent;
    }

    public void RegisterLiveAgent(string connectionId, AgentRegistrationDto reg)
    {
        _connectionToAgentId[connectionId] = reg.AgentId;

        if (_agents.TryGetValue(reg.AgentId, out var existing))
        {
            existing.ConnectionId = connectionId;
            existing.Registration = reg;
            existing.LastSeenUtc = DateTime.UtcNow;
        }
        else
        {
            var newAgent = new ConnectedAgent
            {
                AgentId = reg.AgentId,
                ConnectionId = connectionId,
                Registration = reg,
                LastSeenUtc = DateTime.UtcNow,
                Checks = GenerateDefaultChecks(reg.Hostname)
            };
            _agents[reg.AgentId] = newAgent;
        }

        _logger.LogInformation("Agent registered: {Hostname} ({AgentId})", reg.Hostname, reg.AgentId);
        NotifyStateChanged();
    }

    public void UpdateHeartbeat(string connectionId, AgentHeartbeatDto heartbeat)
    {
        if (string.IsNullOrEmpty(heartbeat.AgentId) && _connectionToAgentId.TryGetValue(connectionId, out var mappedId))
        {
            heartbeat.AgentId = mappedId;
        }

        if (!string.IsNullOrEmpty(heartbeat.AgentId) && _agents.TryGetValue(heartbeat.AgentId, out var agent))
        {
            agent.LatestHeartbeat = heartbeat;
            agent.LastSeenUtc = DateTime.UtcNow;
            agent.ConnectionId = connectionId;
            NotifyStateChanged();
        }
    }

    public void HandleAgentDisconnected(string connectionId)
    {
        if (_connectionToAgentId.TryRemove(connectionId, out var agentId))
        {
            if (_agents.TryGetValue(agentId, out var agent))
            {
                agent.ConnectionId = null;
                agent.LastSeenUtc = DateTime.UtcNow;
                NotifyStateChanged();
            }
        }
    }

    public void ReceiveCommandOutput(CommandOutputChunkDto chunk)
    {
        OnCommandOutput?.Invoke(chunk);
    }

    public void ReceiveDirectoryListing(DirectoryListingDto listing)
    {
        if (_agents.TryGetValue(listing.RequestId, out var agent))
        {
            agent.CurrentDirectory = listing;
        }
        OnDirectoryListing?.Invoke(listing);
        NotifyStateChanged();
    }

    public void ReceiveFileContent(FileContentResponseDto response)
    {
        OnFileContent?.Invoke(response);
    }

    public void ReceiveFileOpResult(FileOperationResultDto result)
    {
        OnFileOpResult?.Invoke(result);
    }

    public void ReceiveProcesses(string requestId, List<ProcessDto> processes)
    {
        if (_agents.TryGetValue(requestId, out var agent))
        {
            agent.Processes = processes;
        }
        OnProcessesUpdated?.Invoke(requestId, processes);
        NotifyStateChanged();
    }

    public void ReceiveServices(string requestId, List<ServiceItemDto> services)
    {
        if (_agents.TryGetValue(requestId, out var agent))
        {
            agent.Services = services;
        }
        OnServicesUpdated?.Invoke(requestId, services);
        NotifyStateChanged();
    }

    public void ReceiveScreenFrame(ScreenFrameDto frame)
    {
        if (_agents.TryGetValue(frame.AgentId, out var agent))
        {
            agent.LatestScreen = frame;
        }
        OnScreenFrame?.Invoke(frame);
    }

    public void ReceiveChecks(string requestId, List<SystemCheckDto> checks)
    {
        if (_agents.TryGetValue(requestId, out var agent))
        {
            agent.Checks = checks;
        }
        OnChecksUpdated?.Invoke(requestId, checks);
        NotifyStateChanged();
    }

    public void ReceiveSoftware(string requestId, List<InstalledSoftwareDto> software)
    {
        if (_agents.TryGetValue(requestId, out var agent))
        {
            agent.Software = software;
        }
        OnSoftwareUpdated?.Invoke(requestId, software);
        NotifyStateChanged();
    }

    // Server-to-Agent RPC commands
    public async Task ExecuteCommandAsync(string agentId, string command, string shell = "PowerShell", string workDir = "")
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            var req = new CommandRequestDto
            {
                AgentId = agentId,
                Command = command,
                Shell = shell,
                WorkingDirectory = workDir
            };
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("ExecuteCommand", req);
        }
        else
        {
            // Simulated response for offline/seed agent
            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                ReceiveCommandOutput(new CommandOutputChunkDto
                {
                    AgentId = agentId,
                    Text = $"[Simulated Shell: {shell}] {command}\r\nCommand completed successfully on {agent?.Hostname ?? "Target"}.",
                    IsCompleted = true,
                    ExitCode = 0
                });
            });
        }
    }

    public async Task BrowseDirectoryAsync(string agentId, string path)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("BrowseDirectory", agentId, path);
        }
        else
        {
            // Default sample directory
            var sample = new DirectoryListingDto
            {
                RequestId = agentId,
                CurrentPath = string.IsNullOrEmpty(path) ? "C:\\" : path,
                ParentPath = "C:\\",
                Drives = [
                    new DriveItemDto { Name = "C:\\", VolumeLabel = "OS_Disk", TotalBytes = 512L * 1024 * 1024 * 1024, FreeBytes = 144L * 1024 * 1024 * 1024 },
                    new DriveItemDto { Name = "D:\\", VolumeLabel = "Data", TotalBytes = 1024L * 1024 * 1024 * 1024, FreeBytes = 620L * 1024 * 1024 * 1024 }
                ],
                Items = [
                    new FileItemDto { Name = "Program Files", Path = "C:\\Program Files", IsDirectory = true, LastModifiedUtc = DateTime.UtcNow.AddDays(-10) },
                    new FileItemDto { Name = "Program Files (x86)", Path = "C:\\Program Files (x86)", IsDirectory = true, LastModifiedUtc = DateTime.UtcNow.AddDays(-10) },
                    new FileItemDto { Name = "Users", Path = "C:\\Users", IsDirectory = true, LastModifiedUtc = DateTime.UtcNow.AddDays(-5) },
                    new FileItemDto { Name = "Windows", Path = "C:\\Windows", IsDirectory = true, LastModifiedUtc = DateTime.UtcNow.AddDays(-2) },
                    new FileItemDto { Name = "vi-rmm-agent-log.txt", Path = "C:\\vi-rmm-agent-log.txt", IsDirectory = false, SizeBytes = 4096, LastModifiedUtc = DateTime.UtcNow }
                ]
            };
            ReceiveDirectoryListing(sample);
        }
    }

    public async Task GetFileContentAsync(string agentId, string path)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetFileContent", agentId, path);
        }
        else
        {
            ReceiveFileContent(new FileContentResponseDto
            {
                RequestId = agentId,
                Path = path,
                TextContent = $"[File Content Preview for: {path}]\nGenerated by Vi-RMM Console\nTimestamp: {DateTime.UtcNow:O}\nAll endpoint services operating normally."
            });
        }
    }

    public async Task UploadFileAsync(string agentId, string path, byte[] content)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("UploadFile", agentId, path, content);
        }
        else
        {
            ReceiveFileOpResult(new FileOperationResultDto { RequestId = agentId, Success = true, Message = "File uploaded successfully." });
        }
    }

    public async Task DeleteFileAsync(string agentId, string path)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("DeleteFile", agentId, path);
        }
        else
        {
            ReceiveFileOpResult(new FileOperationResultDto { RequestId = agentId, Success = true, Message = "Deleted successfully." });
        }
    }

    public async Task CreateFolderAsync(string agentId, string path)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("CreateFolder", agentId, path);
        }
        else
        {
            ReceiveFileOpResult(new FileOperationResultDto { RequestId = agentId, Success = true, Message = "Folder created." });
        }
    }

    public async Task GetProcessesAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetProcesses", agentId);
        }
    }

    public async Task KillProcessAsync(string agentId, int pid)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("KillProcess", agentId, pid);
        }
    }

    public async Task GetServicesAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetServices", agentId);
        }
    }

    public async Task ControlServiceAsync(string agentId, string serviceName, string action)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("ControlService", agentId, serviceName, action);
        }
    }

    public async Task CaptureScreenAsync(string agentId, int quality = 65)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("CaptureScreen", agentId, quality);
        }
    }

    public async Task SendInputAsync(string agentId, RemoteInputDto input)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("SendRemoteInput", input);
        }
    }

    public async Task GetChecksAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetChecks", agentId);
        }
    }

    public async Task GetSoftwareAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetSoftware", agentId);
        }
    }

    public async Task RebootAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("SystemAction", "reboot");
        }
    }

    public void SaveNotes(string agentId, string notes)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Notes = notes;
            NotifyStateChanged();
        }
    }

    public void RemoveAgent(string agentId)
    {
        _agents.TryRemove(agentId, out _);
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnChange?.Invoke();
    }

    private void SeedDefaultAgents()
    {
        var list = new List<ConnectedAgent>
        {
            new()
            {
                AgentId = "demo-pc-cpt-001",
                Registration = new AgentRegistrationDto
                {
                    AgentId = "demo-pc-cpt-001",
                    Hostname = "PC-CPT-001",
                    ClientName = "Company 1",
                    SiteName = "Cape Town HQ",
                    OperatingSystem = "Windows 11 Pro (23H2)",
                    ProcessorName = "Intel Core i7-12700K",
                    CoreCount = 12,
                    TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
                    TotalDiskBytes = 512L * 1024 * 1024 * 1024,
                    FreeDiskBytes = 144L * 1024 * 1024 * 1024,
                    LoggedUser = "Ashley",
                    LocalIp = "192.168.1.104",
                    PublicIp = "102.132.88.12",
                    AgentVersion = "1.0.0",
                    IsLiveAgent = false,
                    Drives = [
                        new DriveItemDto { Name = "C:", VolumeLabel = "OS", TotalBytes = 512L * 1024 * 1024 * 1024, FreeBytes = 144L * 1024 * 1024 * 1024 }
                    ]
                },
                LatestHeartbeat = new AgentHeartbeatDto
                {
                    AgentId = "demo-pc-cpt-001",
                    CpuUsagePercent = 8.4,
                    UsedMemoryBytes = 7L * 1024 * 1024 * 1024,
                    TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
                    FreeMemoryBytes = 9L * 1024 * 1024 * 1024,
                    FreeDiskBytes = 144L * 1024 * 1024 * 1024,
                    TotalDiskBytes = 512L * 1024 * 1024 * 1024,
                    LoggedUser = "Ashley",
                    UpTime = TimeSpan.FromHours(78),
                    ProcessCount = 142,
                    ServiceCount = 89,
                    HealthyChecksCount = 9,
                    TotalChecksCount = 9,
                    Status = "Online"
                },
                Checks = GenerateDefaultChecks("PC-CPT-001"),
                Processes = GenerateDefaultProcesses(),
                Services = GenerateDefaultServices(),
                Software = GenerateDefaultSoftware(),
                Patches = GenerateDefaultPatches()
            },
            new()
            {
                AgentId = "demo-pc-cpt-002",
                Registration = new AgentRegistrationDto
                {
                    AgentId = "demo-pc-cpt-002",
                    Hostname = "PC-CPT-002",
                    ClientName = "Company 2",
                    SiteName = "Cape Town HQ",
                    OperatingSystem = "Windows 11 Enterprise",
                    ProcessorName = "AMD Ryzen 7 5800X",
                    CoreCount = 8,
                    TotalMemoryBytes = 32L * 1024 * 1024 * 1024,
                    TotalDiskBytes = 1024L * 1024 * 1024 * 1024,
                    FreeDiskBytes = 480L * 1024 * 1024 * 1024,
                    LoggedUser = "John",
                    LocalIp = "192.168.1.105",
                    AgentVersion = "1.0.0",
                    IsLiveAgent = false
                },
                LatestHeartbeat = new AgentHeartbeatDto
                {
                    AgentId = "demo-pc-cpt-002",
                    CpuUsagePercent = 14.2,
                    UsedMemoryBytes = 12L * 1024 * 1024 * 1024,
                    TotalMemoryBytes = 32L * 1024 * 1024 * 1024,
                    Status = "Online"
                },
                Checks = GenerateDefaultChecks("PC-CPT-002"),
                Processes = GenerateDefaultProcesses(),
                Services = GenerateDefaultServices()
            },
            new()
            {
                AgentId = "demo-srv-lon-001",
                Registration = new AgentRegistrationDto
                {
                    AgentId = "demo-srv-lon-001",
                    Hostname = "SRV-LON-001",
                    ClientName = "Company 3",
                    SiteName = "London Office",
                    OperatingSystem = "Windows Server 2022 Datacenter",
                    ProcessorName = "Intel Xeon Gold 6330",
                    CoreCount = 28,
                    TotalMemoryBytes = 64L * 1024 * 1024 * 1024,
                    TotalDiskBytes = 2048L * 1024 * 1024 * 1024,
                    FreeDiskBytes = 85L * 1024 * 1024 * 1024,
                    LoggedUser = "System",
                    LocalIp = "10.0.4.15",
                    AgentVersion = "1.0.0",
                    IsLiveAgent = false
                },
                LatestHeartbeat = new AgentHeartbeatDto
                {
                    AgentId = "demo-srv-lon-001",
                    CpuUsagePercent = 78.5,
                    UsedMemoryBytes = 52L * 1024 * 1024 * 1024,
                    TotalMemoryBytes = 64L * 1024 * 1024 * 1024,
                    Status = "Warning"
                },
                Checks = GenerateDefaultChecks("SRV-LON-001", true),
                Processes = GenerateDefaultProcesses(),
                Services = GenerateDefaultServices()
            },
            new()
            {
                AgentId = "demo-pc-ny-014",
                Registration = new AgentRegistrationDto
                {
                    AgentId = "demo-pc-ny-014",
                    Hostname = "PC-NY-014",
                    ClientName = "Company 4",
                    SiteName = "New York",
                    OperatingSystem = "Windows 10 Pro",
                    ProcessorName = "Intel Core i5-11400",
                    CoreCount = 6,
                    TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
                    TotalDiskBytes = 512L * 1024 * 1024 * 1024,
                    FreeDiskBytes = 210L * 1024 * 1024 * 1024,
                    LoggedUser = "Sarah",
                    LocalIp = "192.168.20.44",
                    AgentVersion = "1.0.0",
                    IsLiveAgent = false
                },
                LatestHeartbeat = new AgentHeartbeatDto
                {
                    AgentId = "demo-pc-ny-014",
                    CpuUsagePercent = 4.1,
                    UsedMemoryBytes = 6L * 1024 * 1024 * 1024,
                    TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
                    Status = "Online"
                },
                Checks = GenerateDefaultChecks("PC-NY-014"),
                Processes = GenerateDefaultProcesses(),
                Services = GenerateDefaultServices()
            },
            new()
            {
                AgentId = "demo-pc-cpt-018",
                Registration = new AgentRegistrationDto
                {
                    AgentId = "demo-pc-cpt-018",
                    Hostname = "PC-CPT-018",
                    ClientName = "Company 5",
                    SiteName = "Cape Town",
                    OperatingSystem = "Windows 11 Home",
                    ProcessorName = "AMD Ryzen 5 5600G",
                    CoreCount = 6,
                    TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
                    TotalDiskBytes = 512L * 1024 * 1024 * 1024,
                    FreeDiskBytes = 320L * 1024 * 1024 * 1024,
                    LoggedUser = "Karen",
                    LocalIp = "192.168.1.118",
                    AgentVersion = "1.0.0",
                    IsLiveAgent = false
                },
                LatestHeartbeat = new AgentHeartbeatDto
                {
                    AgentId = "demo-pc-cpt-018",
                    Status = "Offline"
                },
                Checks = GenerateDefaultChecks("PC-CPT-018"),
                Processes = GenerateDefaultProcesses(),
                Services = GenerateDefaultServices()
            }
        };

        foreach (var agent in list)
        {
            _agents[agent.AgentId] = agent;
        }
    }

    private static List<SystemCheckDto> GenerateDefaultChecks(string hostname, bool isWarning = false) =>
    [
        new() { Name = "Windows Defender Antivirus", Category = "Security", IsPassing = true, StatusText = "Passing", Details = "Real-time protection enabled, definitions up-to-date." },
        new() { Name = "Windows Firewall", Category = "Security", IsPassing = true, StatusText = "Passing", Details = "Domain, Private, and Public profiles are active." },
        new() { Name = "Disk Space (Drive C:)", Category = "Storage", IsPassing = !isWarning, StatusText = isWarning ? "Warning (<15% free)" : "Passing", Details = isWarning ? "Low disk space alert on OS drive." : "28% free space available." },
        new() { Name = "CPU Load Check", Category = "Performance", IsPassing = !isWarning, StatusText = isWarning ? "High Load" : "Passing", Details = isWarning ? "CPU sustained above 75% for 10 minutes." : "Normal average load." },
        new() { Name = "Memory Utilization", Category = "Performance", IsPassing = true, StatusText = "Passing", Details = "Healthy memory usage." },
        new() { Name = "Windows Update Service", Category = "System", IsPassing = true, StatusText = "Passing", Details = "wuauserv service is running." },
        new() { Name = "Pending Reboot Check", Category = "System", IsPassing = true, StatusText = "Passing", Details = "No reboot required." },
        new() { Name = "Event Log Errors", Category = "System", IsPassing = true, StatusText = "Passing", Details = "0 critical system errors in last 24 hours." },
        new() { Name = "Vi-RMM Agent Service", Category = "System", IsPassing = true, StatusText = "Passing", Details = "Agent daemon heartbeating reliably." }
    ];

    private static List<ProcessDto> GenerateDefaultProcesses() =>
    [
        new() { Id = 4, Name = "System", WorkingSetBytes = 120L * 1024 * 1024, CpuPercent = 0.5, ThreadCount = 210, UserName = "SYSTEM" },
        new() { Id = 1240, Name = "explorer.exe", WorkingSetBytes = 85L * 1024 * 1024, CpuPercent = 1.2, ThreadCount = 64, UserName = "Ashley", MainWindowTitle = "Explorer" },
        new() { Id = 3492, Name = "chrome.exe", WorkingSetBytes = 450L * 1024 * 1024, CpuPercent = 3.4, ThreadCount = 48, UserName = "Ashley", MainWindowTitle = "Vi-RMM Dashboard - Google Chrome" },
        new() { Id = 5120, Name = "Vi-RMM.Agent.exe", WorkingSetBytes = 28L * 1024 * 1024, CpuPercent = 0.1, ThreadCount = 12, UserName = "SYSTEM" },
        new() { Id = 6840, Name = "powershell.exe", WorkingSetBytes = 64L * 1024 * 1024, CpuPercent = 0.0, ThreadCount = 18, UserName = "Ashley" },
        new() { Id = 8900, Name = "svchost.exe", WorkingSetBytes = 42L * 1024 * 1024, CpuPercent = 0.2, ThreadCount = 24, UserName = "NETWORK SERVICE" }
    ];

    private static List<ServiceItemDto> GenerateDefaultServices() =>
    [
        new() { Name = "ViRMMAgent", DisplayName = "Vi-RMM Agent", Status = "Running", StartType = "Automatic", CanStop = true },
        new() { Name = "WinDefend", DisplayName = "Microsoft Defender Antivirus Service", Status = "Running", StartType = "Automatic", CanStop = false },
        new() { Name = "wuauserv", DisplayName = "Windows Update", Status = "Running", StartType = "Manual", CanStop = true },
        new() { Name = "Spooler", DisplayName = "Print Spooler", Status = "Running", StartType = "Automatic", CanStop = true },
        new() { Name = "LanmanServer", DisplayName = "Server", Status = "Running", StartType = "Automatic", CanStop = true },
        new() { Name = "Dnscache", DisplayName = "DNS Client", Status = "Running", StartType = "Automatic", CanStop = false },
        new() { Name = "BITS", DisplayName = "Background Intelligent Transfer Service", Status = "Running", StartType = "Automatic", CanStop = true },
        new() { Name = "RemoteRegistry", DisplayName = "Remote Registry", Status = "Stopped", StartType = "Disabled", CanStop = false }
    ];

    private static List<InstalledSoftwareDto> GenerateDefaultSoftware() =>
    [
        new() { Name = "Google Chrome", Version = "127.0.6533.89", Publisher = "Google LLC", InstallDate = "2026-08-01", SizeFormatted = "280 MB" },
        new() { Name = "Vi-RMM Endpoint Agent", Version = "1.0.0", Publisher = "Vi-RMM", InstallDate = "2026-08-13", SizeFormatted = "18 MB" },
        new() { Name = "Microsoft Visual Studio Code", Version = "1.92.0", Publisher = "Microsoft Corporation", InstallDate = "2026-07-20", SizeFormatted = "340 MB" },
        new() { Name = ".NET Runtime 10.0.0 (x64)", Version = "10.0.0", Publisher = "Microsoft Corporation", InstallDate = "2026-08-10", SizeFormatted = "140 MB" },
        new() { Name = "7-Zip 24.07 (x64)", Version = "24.07", Publisher = "Igor Pavlov", InstallDate = "2026-06-15", SizeFormatted = "5.2 MB" }
    ];

    private static List<WindowsPatchDto> GenerateDefaultPatches() =>
    [
        new() { KbArticleId = "KB5041585", Title = "2026-08 Cumulative Update for Windows 11 Version 23H2", Severity = "Critical", Status = "Installed", InstalledOnUtc = DateTime.UtcNow.AddDays(-5) },
        new() { KbArticleId = "KB5041080", Title = "2026-08 Security Update for .NET Framework 4.8.1", Severity = "Important", Status = "Installed", InstalledOnUtc = DateTime.UtcNow.AddDays(-5) },
        new() { KbArticleId = "KB890830", Title = "Windows Malicious Software Removal Tool x64 - v5.127", Severity = "Moderate", Status = "Installed", InstalledOnUtc = DateTime.UtcNow.AddDays(-3) }
    ];
}
