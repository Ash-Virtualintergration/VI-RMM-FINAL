using Microsoft.Win32;
using ViRMM.Shared.Models;

namespace ViRMM.Agent.Services;

public class HealthChecker
{
    public List<SystemCheckDto> RunChecks()
    {
        var checks = new List<SystemCheckDto>();

        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady))
        {
            var freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
            checks.Add(new SystemCheckDto
            {
                Name = $"Disk Space ({drive.Name})",
                Category = "Storage",
                IsPassing = freePercent >= 15.0,
                StatusText = $"{freePercent:F1}% free",
                Details = $"Drive {drive.Name} has {drive.AvailableFreeSpace:N0} bytes free of {drive.TotalSize:N0} bytes."
            });
        }

        return checks;
    }

    public List<InstalledSoftwareDto> GetInstalledSoftware()
    {
        var software = new List<InstalledSoftwareDto>();
        if (!OperatingSystem.IsWindows()) return software;

        try
        {
            string[] registryKeys =
            [
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            ];

            foreach (var keyPath in registryKeys)
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                if (key == null) continue;

                foreach (var subkeyName in key.GetSubKeyNames())
                {
                    using var subkey = key.OpenSubKey(subkeyName);
                    if (subkey == null) continue;

                    var name = subkey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var version = subkey.GetValue("DisplayVersion") as string ?? "";
                    var publisher = subkey.GetValue("Publisher") as string ?? "";
                    var installDate = subkey.GetValue("InstallDate") as string ?? "";

                    software.Add(new InstalledSoftwareDto
                    {
                        Name = name,
                        Version = version,
                        Publisher = publisher,
                        InstallDate = installDate
                    });
                }
            }
        }
        catch { }

        return software.DistinctBy(s => s.Name).OrderBy(s => s.Name).ToList();
    }
}
