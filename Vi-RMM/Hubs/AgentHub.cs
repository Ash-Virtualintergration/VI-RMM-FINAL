using Microsoft.AspNetCore.SignalR;
using ViRMM.Services;
using ViRMM.Shared.Models;

namespace ViRMM.Hubs;

public class AgentHub : Hub
{
    private readonly AgentService _agentService;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(AgentService agentService, ILogger<AgentHub> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Agent connected with ConnectionId: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Agent disconnected with ConnectionId: {ConnectionId}", Context.ConnectionId);
        _agentService.HandleAgentDisconnected(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public void RegisterAgent(AgentRegistrationDto registration)
    {
        _agentService.RegisterLiveAgent(Context.ConnectionId, registration);
    }

    public void SendHeartbeat(AgentHeartbeatDto heartbeat)
    {
        _agentService.UpdateHeartbeat(Context.ConnectionId, heartbeat);
    }

    public void SendCommandOutput(CommandOutputChunkDto chunk)
    {
        _agentService.ReceiveCommandOutput(chunk);
    }

    public void SendDirectoryListing(DirectoryListingDto listing)
    {
        _agentService.ReceiveDirectoryListing(listing);
    }

    public void SendFileContent(FileContentResponseDto response)
    {
        _agentService.ReceiveFileContent(response);
    }

    public void SendFileOpResult(FileOperationResultDto result)
    {
        _agentService.ReceiveFileOpResult(result);
    }

    public void SendProcessList(string requestId, List<ProcessDto> processes)
    {
        _agentService.ReceiveProcesses(requestId, processes);
    }

    public void SendServiceList(string requestId, List<ServiceItemDto> services)
    {
        _agentService.ReceiveServices(requestId, services);
    }

    public void SendScreenFrame(ScreenFrameDto frame)
    {
        _agentService.ReceiveScreenFrame(frame);
    }

    public void SendChecks(string requestId, List<SystemCheckDto> checks)
    {
        _agentService.ReceiveChecks(requestId, checks);
    }

    public void SendSoftware(string requestId, List<InstalledSoftwareDto> software)
    {
        _agentService.ReceiveSoftware(requestId, software);
    }
}
