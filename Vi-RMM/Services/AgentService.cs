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
    public string Status => IsLiveConnected ? (LatestHeartbeat?.Status ?? "Connected") : "Offline";
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public string Client => Registration.ClientName;
    public string Site => Registration.SiteName;
    public string Hostname => Registration.Hostname;
    public string User => LatestHeartbeat?.LoggedUser ?? Registration.LoggedUser;
    public string OperatingSystem => Registration.OperatingSystem;
    public double CpuPercent => LatestHeartbeat?.CpuUsagePercent ?? 0;
    public double MemoryUsedPercent
    {
        get
        {
            if (Registration.TotalMemoryBytes <= 0 || LatestHeartbeat is null) return 0;
            var used = LatestHeartbeat.UsedMemoryBytes;
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
                LastSeenUtc = DateTime.UtcNow
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
            _logger.LogWarning("Cannot execute command: agent {AgentId} is not connected.", agentId);
            ReceiveCommandOutput(new CommandOutputChunkDto
            {
                AgentId = agentId,
                Text = "Command not sent: endpoint is not connected.",
                IsError = true,
                IsCompleted = true,
                ExitCode = -1
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
            _logger.LogWarning("Cannot browse files: agent {AgentId} is not connected.", agentId);
            ReceiveDirectoryListing(new DirectoryListingDto
            {
                RequestId = agentId,
                CurrentPath = path,
                Error = "Endpoint is not connected."
            });
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
            _logger.LogWarning("Cannot read file: agent {AgentId} is not connected.", agentId);
            ReceiveFileContent(new FileContentResponseDto
            {
                RequestId = agentId,
                Path = path,
                Error = "Endpoint is not connected."
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
            _logger.LogWarning("Cannot upload file: agent {AgentId} is not connected.", agentId);
            ReceiveFileOpResult(new FileOperationResultDto { RequestId = agentId, Success = false, Message = "Endpoint is not connected." });
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
            _logger.LogWarning("Cannot delete file: agent {AgentId} is not connected.", agentId);
            ReceiveFileOpResult(new FileOperationResultDto { RequestId = agentId, Success = false, Message = "Endpoint is not connected." });
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
            _logger.LogWarning("Cannot create folder: agent {AgentId} is not connected.", agentId);
            ReceiveFileOpResult(new FileOperationResultDto { RequestId = agentId, Success = false, Message = "Endpoint is not connected." });
        }
    }

    public async Task GetProcessesAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetProcesses", agentId);
        }
        else _logger.LogWarning("Cannot retrieve processes: agent {AgentId} is not connected.", agentId);
    }

    public async Task KillProcessAsync(string agentId, int pid)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("KillProcess", agentId, pid);
        }
        else _logger.LogWarning("Cannot terminate process: agent {AgentId} is not connected.", agentId);
    }

    public async Task GetServicesAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetServices", agentId);
        }
        else _logger.LogWarning("Cannot retrieve services: agent {AgentId} is not connected.", agentId);
    }

    public async Task ControlServiceAsync(string agentId, string serviceName, string action)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("ControlService", agentId, serviceName, action);
        }
        else _logger.LogWarning("Cannot control service: agent {AgentId} is not connected.", agentId);
    }

    public async Task CaptureScreenAsync(string agentId, int quality = 65)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("CaptureScreen", agentId, quality);
        }
        else _logger.LogWarning("Cannot capture screen: agent {AgentId} is not connected.", agentId);
    }

    public async Task SendInputAsync(string agentId, RemoteInputDto input)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("SendRemoteInput", input);
        }
        else _logger.LogWarning("Cannot send remote input: agent {AgentId} is not connected.", agentId);
    }

    public async Task GetChecksAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetChecks", agentId);
        }
        else _logger.LogWarning("Cannot retrieve checks: agent {AgentId} is not connected.", agentId);
    }

    public async Task GetSoftwareAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("GetSoftware", agentId);
        }
        else _logger.LogWarning("Cannot retrieve software inventory: agent {AgentId} is not connected.", agentId);
    }

    public async Task RebootAsync(string agentId)
    {
        var agent = GetAgent(agentId);
        if (agent?.ConnectionId != null)
        {
            await _hubContext.Clients.Client(agent.ConnectionId).SendAsync("SystemAction", "reboot");
        }
        else _logger.LogWarning("Cannot reboot: agent {AgentId} is not connected.", agentId);
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

}
