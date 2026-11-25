using System.Diagnostics;
using System.Security.Principal;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ViRMM.Agent.Services;
using ViRMM.Shared.Models;

const string ServiceName = "ViRMMAgent";

// Parse CLI arguments
string? customServerUrl = null;
string clientName = "";
string siteName = "";
string agentId = GetOrCreateAgentId();

// Try loading saved config from %ProgramData%\Vi-RMM\config.txt if it exists
var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Vi-RMM");
var configFilePath = Path.Combine(appDataDir, "config.txt");
if (File.Exists(configFilePath))
{
    try
    {
        var lines = File.ReadAllLines(configFilePath);
        foreach (var line in lines)
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var val = parts[1].Trim();
                if (key.Equals("server", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) customServerUrl = val;
                if (key.Equals("client", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) clientName = val;
                if (key.Equals("site", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) siteName = val;
            }
        }
    }
    catch { }
}

for (int i = 0; i < args.Length; i++)
{
    if (string.Equals(args[i], "--server", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        customServerUrl = args[++i].TrimEnd('/');
    }
    else if (string.Equals(args[i], "--client", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        clientName = args[++i];
    }
    else if (string.Equals(args[i], "--site", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        siteName = args[++i];
    }
}

// Default list of candidate server URLs to auto-detect if not explicitly configured
string[] candidateUrls = string.IsNullOrEmpty(customServerUrl)
    ? ["https://localhost:7250", "http://localhost:5250", "http://localhost:5000", "https://localhost:5001"]
    : [customServerUrl];

if (args.Any(a => string.Equals(a, "--install", StringComparison.OrdinalIgnoreCase)))
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("The Vi-RMM Agent installer can only run on Windows.");
        Environment.Exit(2);
    }

    if (!IsAdministrator())
    {
        Console.Error.WriteLine("Please run Vi-RMM.Agent.exe as Administrator to install the service.");
        Environment.Exit(3);
    }

    var exePath = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(exePath))
    {
        Console.Error.WriteLine("Unable to determine the agent executable path.");
        Environment.Exit(4);
    }

    try
    {
        // Save config file
        Directory.CreateDirectory(appDataDir);
        var targetServer = customServerUrl ?? "https://localhost:7250";
        File.WriteAllLines(configFilePath, [$"server={targetServer}", $"client={clientName}", $"site={siteName}"]);

        var binArgs = $"\\\"{exePath}\\\" --server {targetServer} --client \\\"{clientName}\\\" --site \\\"{siteName}\\\"";
        RunSc($"create {ServiceName} binPath= \"{binArgs}\" start= auto DisplayName= \"Vi-RMM Agent\"");
        RunSc($"description {ServiceName} \"Vi-RMM Endpoint Management & Remote Monitoring Agent\"");
        RunSc($"start {ServiceName}");

        Console.WriteLine($"Vi-RMM Agent installed and started. Server configured to: {targetServer}");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Installation failed: {ex.Message}");
        Environment.Exit(5);
    }
}

if (args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)))
{
    if (!IsAdministrator())
    {
        Console.Error.WriteLine("Please run Vi-RMM.Agent.exe as Administrator to uninstall.");
        Environment.Exit(3);
    }

    try
    {
        RunSc($"stop {ServiceName}");
        RunSc($"delete {ServiceName}");
        Console.WriteLine("Vi-RMM Agent service removed.");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Uninstall error: {ex.Message}");
        Environment.Exit(6);
    }
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Vi-RMM Agent";
});

builder.Services.AddSingleton(new AgentConfig
{
    AgentId = agentId,
    CandidateUrls = candidateUrls,
    ClientName = clientName,
    SiteName = siteName,
    Version = "1.0.0"
});

builder.Services.AddSingleton<TelemetryCollector>();
builder.Services.AddSingleton<CommandExecutor>();
builder.Services.AddSingleton<RemoteFileSystem>();
builder.Services.AddSingleton<ProcessServiceManager>();
builder.Services.AddSingleton<ScreenCaptureService>();
builder.Services.AddSingleton<HealthChecker>();
builder.Services.AddHostedService<ViRmmWorker>();

await builder.Build().RunAsync();

static string GetOrCreateAgentId()
{
    try
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(appData, "Vi-RMM");
        Directory.CreateDirectory(dir);
        var idFile = Path.Combine(dir, "agent.id");
        if (File.Exists(idFile))
        {
            var id = File.ReadAllText(idFile).Trim();
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }

        var newId = "agent-" + Guid.NewGuid().ToString("N")[..12];
        File.WriteAllText(idFile, newId);
        return newId;
    }
    catch
    {
        return "agent-" + Environment.MachineName.ToLowerInvariant();
    }
}

static bool IsAdministrator()
{
    if (!OperatingSystem.IsWindows()) return false;
    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

static void RunSc(string arguments)
{
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "sc.exe",
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    }) ?? throw new InvalidOperationException("Unable to start sc.exe.");

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new InvalidOperationException($"Service operation failed. {output} {error}".Trim());
}

public class AgentConfig
{
    public string AgentId { get; set; } = "";
    public string[] CandidateUrls { get; set; } = [];
    public string ClientName { get; set; } = "";
    public string SiteName { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
}

sealed class ViRmmWorker : BackgroundService
{
    private readonly AgentConfig _config;
    private readonly TelemetryCollector _telemetry;
    private readonly CommandExecutor _commands;
    private readonly RemoteFileSystem _fileSystem;
    private readonly ProcessServiceManager _processService;
    private readonly ScreenCaptureService _screenCapture;
    private readonly HealthChecker _healthChecker;
    private readonly ILogger<ViRmmWorker> _logger;

    public ViRmmWorker(
        AgentConfig config,
        TelemetryCollector telemetry,
        CommandExecutor commands,
        RemoteFileSystem fileSystem,
        ProcessServiceManager processService,
        ScreenCaptureService screenCapture,
        HealthChecker healthChecker,
        ILogger<ViRmmWorker> logger)
    {
        _config = config;
        _telemetry = telemetry;
        _commands = commands;
        _fileSystem = fileSystem;
        _processService = processService;
        _screenCapture = screenCapture;
        _healthChecker = healthChecker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Vi-RMM Agent started on {Machine} (ID: {AgentId})", Environment.MachineName, _config.AgentId);

        HubConnection? activeHubConnection = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (activeHubConnection == null || activeHubConnection.State == HubConnectionState.Disconnected)
            {
                foreach (var baseUrl in _config.CandidateUrls)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var hubUrl = $"{baseUrl.TrimEnd('/')}/agentHub";
                    _logger.LogInformation("Attempting connection to Vi-RMM Hub at {HubUrl}...", hubUrl);

                    var testConnection = new HubConnectionBuilder()
                        .WithUrl(hubUrl, options =>
                        {
                            // Accept local dev self-signed certificates
                            options.HttpMessageHandlerFactory = handler =>
                            {
                                if (handler is HttpClientHandler clientHandler)
                                {
                                    clientHandler.ServerCertificateCustomValidationCallback =
                                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                                }
                                return handler;
                            };
                        })
                        .WithAutomaticReconnect()
                        .Build();

                    RegisterHandlers(testConnection, stoppingToken);

                    try
                    {
                        await testConnection.StartAsync(stoppingToken);
                        _logger.LogInformation("Successfully connected to Vi-RMM Hub at {HubUrl}!", hubUrl);

                        activeHubConnection = testConnection;

                        var reg = _telemetry.CollectRegistration(_config.AgentId, _config.ClientName, _config.SiteName, _config.Version);
                        await activeHubConnection.InvokeAsync("RegisterAgent", reg, stoppingToken);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Connection attempt to {HubUrl} failed: {Error}", hubUrl, ex.Message);
                        try { await testConnection.DisposeAsync(); } catch { }
                    }
                }

                if (activeHubConnection == null || activeHubConnection.State != HubConnectionState.Connected)
                {
                    _logger.LogWarning("No reachable Vi-RMM server found on configured URLs ({Urls}). Retrying in 5 seconds...", string.Join(", ", _config.CandidateUrls));
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }
            }

            if (activeHubConnection != null && activeHubConnection.State == HubConnectionState.Connected)
            {
                try
                {
                    var heartbeat = _telemetry.CollectHeartbeat(_config.AgentId);
                    await activeHubConnection.InvokeAsync("SendHeartbeat", heartbeat, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Heartbeat send error: {Error}", ex.Message);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }

        if (activeHubConnection != null)
        {
            try
            {
                await activeHubConnection.StopAsync();
                await activeHubConnection.DisposeAsync();
            }
            catch { }
        }
    }

    private void RegisterHandlers(HubConnection hubConnection, CancellationToken stoppingToken)
    {
        hubConnection.On<CommandRequestDto>("ExecuteCommand", async (req) =>
        {
            _logger.LogInformation("Received command execution request: {Command}", req.Command);
            await _commands.ExecuteCommandAsync(req, async (chunk) =>
            {
                if (hubConnection.State == HubConnectionState.Connected)
                {
                    await hubConnection.InvokeAsync("SendCommandOutput", chunk, stoppingToken);
                }
            });
        });

        hubConnection.On<string, string>("BrowseDirectory", async (requestId, path) =>
        {
            var listing = _fileSystem.Browse(requestId, path);
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendDirectoryListing", listing, stoppingToken);
            }
        });

        hubConnection.On<string, string>("GetFileContent", async (requestId, path) =>
        {
            var content = _fileSystem.ReadFileContent(requestId, path);
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendFileContent", content, stoppingToken);
            }
        });

        hubConnection.On<string, string, byte[]>("UploadFile", async (requestId, path, content) =>
        {
            var res = _fileSystem.WriteFile(requestId, path, content);
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendFileOpResult", res, stoppingToken);
            }
        });

        hubConnection.On<string, string>("DeleteFile", async (requestId, path) =>
        {
            var res = _fileSystem.Delete(requestId, path);
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendFileOpResult", res, stoppingToken);
            }
        });

        hubConnection.On<string, string>("CreateFolder", async (requestId, path) =>
        {
            var res = _fileSystem.CreateFolder(requestId, path);
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendFileOpResult", res, stoppingToken);
            }
        });

        hubConnection.On<string>("GetProcesses", async (requestId) =>
        {
            var list = _processService.GetProcesses();
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendProcessList", requestId, list, stoppingToken);
            }
        });

        hubConnection.On<string, int>("KillProcess", (requestId, pid) =>
        {
            _processService.KillProcess(pid);
        });

        hubConnection.On<string>("GetServices", async (requestId) =>
        {
            var list = _processService.GetServices();
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendServiceList", requestId, list, stoppingToken);
            }
        });

        hubConnection.On<string, string, string>("ControlService", (requestId, serviceName, action) =>
        {
            _processService.ControlService(serviceName, action);
        });

        hubConnection.On<string, int>("CaptureScreen", async (requestId, quality) =>
        {
            var frame = _screenCapture.CaptureScreen(_config.AgentId, quality);
            if (frame != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendScreenFrame", frame, stoppingToken);
            }
        });

        hubConnection.On<RemoteInputDto>("SendRemoteInput", (input) =>
        {
            _screenCapture.SimulateInput(input);
        });

        hubConnection.On<string>("GetChecks", async (requestId) =>
        {
            var checks = _healthChecker.RunChecks();
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendChecks", requestId, checks, stoppingToken);
            }
        });

        hubConnection.On<string>("GetSoftware", async (requestId) =>
        {
            var software = _healthChecker.GetInstalledSoftware();
            if (hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendSoftware", requestId, software, stoppingToken);
            }
        });

        hubConnection.On<string>("SystemAction", (action) =>
        {
            if (string.Equals(action, "reboot", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start("shutdown.exe", "/r /t 5 /c \"Reboot initiated by Vi-RMM Console\"");
            }
        });

        hubConnection.Reconnected += async (connectionId) =>
        {
            _logger.LogInformation("Reconnected to Vi-RMM Hub.");
            var reg = _telemetry.CollectRegistration(_config.AgentId, _config.ClientName, _config.SiteName, _config.Version);
            await hubConnection.InvokeAsync("RegisterAgent", reg, stoppingToken);
        };
    }
}
