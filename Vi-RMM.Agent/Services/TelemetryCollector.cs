using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ViRMM.Shared.Models;

namespace ViRMM.Agent.Services;

public class TelemetryCollector
{
    private static DateTime _startTime = DateTime.UtcNow;
    private static TimeSpan _lastCpuTime = TimeSpan.Zero;
    private static DateTime _lastCpuCheck = DateTime.UtcNow;

    public AgentRegistrationDto CollectRegistration(string agentId, string clientName, string siteName, string version)
    {
        var reg = new AgentRegistrationDto
        {
            AgentId = agentId,
            Hostname = Environment.MachineName,
            ClientName = clientName,
            SiteName = siteName,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessorName = GetProcessorName(),
            CoreCount = Environment.ProcessorCount,
            LoggedUser = Environment.UserName,
            AgentVersion = version,
            RegisteredAtUtc = DateTime.UtcNow
        };

        // Memory info
        var mem = GetMemoryStatus();
        reg.TotalMemoryBytes = mem.totalBytes;

        // Drive info
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                reg.Drives.Add(new DriveItemDto
                {
                    Name = d.Name,
                    VolumeLabel = string.IsNullOrEmpty(d.VolumeLabel) ? d.DriveType.ToString() : d.VolumeLabel,
                    FileSystem = d.DriveFormat,
                    TotalBytes = d.TotalSize,
                    FreeBytes = d.AvailableFreeSpace
                });
            }
            reg.TotalDiskBytes = reg.Drives.Sum(d => d.TotalBytes);
            reg.FreeDiskBytes = reg.Drives.Sum(d => d.FreeBytes);
        }
        catch { }

        // Network interfaces
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
            {
                var ipProps = nic.GetIPProperties();
                var ip = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "";

                if (!string.IsNullOrEmpty(ip) && !ip.StartsWith("127."))
                {
                    if (string.IsNullOrEmpty(reg.LocalIp)) reg.LocalIp = ip;
                    reg.NetworkAdapters.Add(new NetworkAdapterDto
                    {
                        Name = nic.Name,
                        IpAddress = ip,
                        MacAddress = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))),
                        Type = nic.NetworkInterfaceType.ToString(),
                        IsUp = true
                    });
                }
            }
        }
        catch { }

        return reg;
    }

    public AgentHeartbeatDto CollectHeartbeat(string agentId)
    {
        var mem = GetMemoryStatus();
        var cpu = GetCpuUsagePercent();

        long totalDisk = 0;
        long freeDisk = 0;
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                totalDisk += d.TotalSize;
                freeDisk += d.AvailableFreeSpace;
            }
        }
        catch { }

        int procCount = 0;
        try { procCount = Process.GetProcesses().Length; } catch { }

        return new AgentHeartbeatDto
        {
            AgentId = agentId,
            CpuUsagePercent = cpu,
            TotalMemoryBytes = mem.totalBytes,
            FreeMemoryBytes = mem.freeBytes,
            UsedMemoryBytes = Math.Max(0, mem.totalBytes - mem.freeBytes),
            TotalDiskBytes = totalDisk,
            FreeDiskBytes = freeDisk,
            LoggedUser = Environment.UserName,
            UpTime = DateTime.UtcNow - _startTime,
            ProcessCount = procCount,
            Status = "Online",
            TimestampUtc = DateTime.UtcNow
        };
    }

    private static string GetProcessorName()
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (!string.IsNullOrEmpty(env)) return env;
        }
        catch { }
        return $"{Environment.ProcessorCount}-Core Processor";
    }

    private static (long totalBytes, long freeBytes) GetMemoryStatus()
    {
        if (OperatingSystem.IsWindows())
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                return ((long)memStatus.ullTotalPhys, (long)memStatus.ullAvailPhys);
            }
        }
        return (0, 0);
    }

    private static double GetCpuUsagePercent()
    {
        try
        {
            var now = DateTime.UtcNow;
            var timePassed = (now - _lastCpuCheck).TotalMilliseconds;
            if (timePassed < 100) return 0;

            var currentCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            _lastCpuCheck = now;
            var cpuUsedMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            _lastCpuTime = currentCpuTime;

            var cpuUsage = (cpuUsedMs / (timePassed * Environment.ProcessorCount)) * 100;
            return Math.Round(Math.Clamp(cpuUsage, 0.0, 100.0), 1);
        }
        catch
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
