using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string ServiceName = "ViRMMAgent";

if (args.Any(a => string.Equals(a, "--install", StringComparison.OrdinalIgnoreCase)))
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("The Vi-RMM Agent installer can only run on Windows.");
        Environment.Exit(2);
    }

    if (!IsAdministrator())
    {
        Console.Error.WriteLine("Please run Vi-RMM.Agent.exe as Administrator.");
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
        RunSc($"create {ServiceName} binPath= \"\\\"{exePath}\\\"\" start= auto DisplayName= \"Vi-RMM Agent\"");
        RunSc($"description {ServiceName} \"Vi-RMM endpoint management agent\"");
        RunSc($"start {ServiceName}");

        Console.WriteLine("Vi-RMM Agent installed and started.");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.Exit(5);
    }
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Vi-RMM Agent";
});

builder.Services.AddHostedService<ViRmmWorker>();

await builder.Build().RunAsync();

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

sealed class ViRmmWorker(ILogger<ViRmmWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Vi-RMM Agent started on {Machine}", Environment.MachineName);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Heartbeat prepared for {Machine} at {Time}",
                Environment.MachineName,
                DateTimeOffset.UtcNow);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
