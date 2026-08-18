using System.Text;
using ViRMM.Shared.Models;

namespace ViRMM.Agent.Services;

public class RemoteFileSystem
{
    public DirectoryListingDto Browse(string requestId, string path)
    {
        var result = new DirectoryListingDto
        {
            RequestId = requestId,
            CurrentPath = string.IsNullOrEmpty(path) ? "C:\\" : path
        };

        try
        {
            // Populate drives
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                result.Drives.Add(new DriveItemDto
                {
                    Name = d.Name,
                    VolumeLabel = string.IsNullOrEmpty(d.VolumeLabel) ? d.DriveType.ToString() : d.VolumeLabel,
                    TotalBytes = d.TotalSize,
                    FreeBytes = d.AvailableFreeSpace
                });
            }

            var dir = new DirectoryInfo(result.CurrentPath);
            if (!dir.Exists)
            {
                result.Error = $"Directory not found: {result.CurrentPath}";
                return result;
            }

            result.ParentPath = dir.Parent?.FullName ?? "";

            foreach (var sub in dir.GetDirectories())
            {
                // Skip hidden/system if inaccessible
                try
                {
                    result.Items.Add(new FileItemDto
                    {
                        Name = sub.Name,
                        Path = sub.FullName,
                        IsDirectory = true,
                        LastModifiedUtc = sub.LastWriteTimeUtc
                    });
                }
                catch { }
            }

            foreach (var file in dir.GetFiles())
            {
                try
                {
                    result.Items.Add(new FileItemDto
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        IsDirectory = false,
                        SizeBytes = file.Length,
                        Extension = file.Extension,
                        LastModifiedUtc = file.LastWriteTimeUtc
                    });
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    public FileContentResponseDto ReadFileContent(string requestId, string path)
    {
        var res = new FileContentResponseDto { RequestId = requestId, Path = path };
        try
        {
            if (!File.Exists(path))
            {
                res.Error = "File not found.";
                return res;
            }

            var info = new FileInfo(path);
            if (info.Length > 2 * 1024 * 1024) // 2MB limit for text preview
            {
                res.TextContent = $"[File exceeds 2MB preview size ({info.Length} bytes). Download the full file to view locally.]";
            }
            else
            {
                res.TextContent = File.ReadAllText(path, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            res.Error = ex.Message;
        }
        return res;
    }

    public FileOperationResultDto WriteFile(string requestId, string path, byte[] content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(path, content);
            return new FileOperationResultDto { RequestId = requestId, Success = true };
        }
        catch (Exception ex)
        {
            return new FileOperationResultDto { RequestId = requestId, Success = false, Message = ex.Message };
        }
    }

    public FileOperationResultDto Delete(string requestId, string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return new FileOperationResultDto { RequestId = requestId, Success = true };
            }
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                return new FileOperationResultDto { RequestId = requestId, Success = true };
            }
            return new FileOperationResultDto { RequestId = requestId, Success = false, Message = "Path not found." };
        }
        catch (Exception ex)
        {
            return new FileOperationResultDto { RequestId = requestId, Success = false, Message = ex.Message };
        }
    }

    public FileOperationResultDto CreateFolder(string requestId, string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return new FileOperationResultDto { RequestId = requestId, Success = true };
        }
        catch (Exception ex)
        {
            return new FileOperationResultDto { RequestId = requestId, Success = false, Message = ex.Message };
        }
    }
}
