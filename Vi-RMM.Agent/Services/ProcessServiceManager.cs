using System.Diagnostics;
using System.ServiceProcess;
using ViRMM.Shared.Models;

namespace ViRMM.Agent.Services;

public class ProcessServiceManager
{
    public List<ProcessDto> GetProcesses()
    {
        var list = new List<ProcessDto>();
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    list.Add(new ProcessDto
                    {
                        Id = p.Id,
                        Name = p.ProcessName,
                        WorkingSetBytes = p.WorkingSet64,
                        ThreadCount = p.Threads.Count,
                        MainWindowTitle = p.MainWindowTitle,
                        Responding = p.Responding
                    });
                }
                catch { }
            }
        }
        catch { }
        return list;
    }

    public bool KillProcess(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            p.Kill(true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public List<ServiceItemDto> GetServices()
    {
        var list = new List<ServiceItemDto>();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var services = ServiceController.GetServices();
                foreach (var s in services)
                {
                    try
                    {
                        list.Add(new ServiceItemDto
                        {
                            Name = s.ServiceName,
                            DisplayName = s.DisplayName,
                            Status = s.Status.ToString(),
                            StartType = s.StartType.ToString(),
                            CanStop = s.CanStop,
                            CanPauseAndContinue = s.CanPauseAndContinue
                        });
                    }
                    catch { }
                }
            }
        }
        catch { }
        return list;
    }

    public bool ControlService(string serviceName, string action)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var sc = new ServiceController(serviceName);
                switch (action.ToLowerInvariant())
                {
                    case "start":
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        }
                        return true;

                    case "stop":
                        if (sc.CanStop && sc.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
                        return true;

                    case "restart":
                        if (sc.CanStop && sc.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        return true;
                }
            }
        }
        catch { }
        return false;
    }
}
