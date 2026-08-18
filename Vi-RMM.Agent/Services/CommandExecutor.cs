using System.Diagnostics;
using System.Text;
using ViRMM.Shared.Models;

namespace ViRMM.Agent.Services;

public class CommandExecutor
{
    public async Task ExecuteCommandAsync(CommandRequestDto req, Func<CommandOutputChunkDto, Task> onOutput)
    {
        var psi = new ProcessStartInfo();
        if (string.Equals(req.Shell, "CMD", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "cmd.exe";
            psi.Arguments = $"/c {req.Command}";
        }
        else
        {
            psi.FileName = "powershell.exe";
            psi.Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{req.Command.Replace("\"", "`\"")}\"";
        }

        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;

        if (!string.IsNullOrEmpty(req.WorkingDirectory) && Directory.Exists(req.WorkingDirectory))
        {
            psi.WorkingDirectory = req.WorkingDirectory;
        }

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += async (_, e) =>
        {
            if (e.Data != null)
            {
                await onOutput(new CommandOutputChunkDto
                {
                    ExecutionId = req.ExecutionId,
                    AgentId = req.AgentId,
                    Text = e.Data,
                    IsError = false
                });
            }
        };

        process.ErrorDataReceived += async (_, e) =>
        {
            if (e.Data != null)
            {
                await onOutput(new CommandOutputChunkDto
                {
                    ExecutionId = req.ExecutionId,
                    AgentId = req.AgentId,
                    Text = e.Data,
                    IsError = true
                });
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(req.TimeoutSeconds > 0 ? req.TimeoutSeconds : 60));
            await process.WaitForExitAsync(cts.Token);

            await onOutput(new CommandOutputChunkDto
            {
                ExecutionId = req.ExecutionId,
                AgentId = req.AgentId,
                IsCompleted = true,
                ExitCode = process.ExitCode
            });
        }
        catch (Exception ex)
        {
            await onOutput(new CommandOutputChunkDto
            {
                ExecutionId = req.ExecutionId,
                AgentId = req.AgentId,
                Text = $"Command execution error: {ex.Message}",
                IsError = true,
                IsCompleted = true,
                ExitCode = -1
            });
        }
    }
}
