namespace ViRMM.Shared.Models;

public class FileItemDto
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string Extension { get; set; } = "";
}

public class DirectoryListingDto
{
    public string RequestId { get; set; } = "";
    public string CurrentPath { get; set; } = "";
    public string ParentPath { get; set; } = "";
    public List<DriveItemDto> Drives { get; set; } = [];
    public List<FileItemDto> Items { get; set; } = [];
    public string? Error { get; set; }
}

public class FileContentResponseDto
{
    public string RequestId { get; set; } = "";
    public string Path { get; set; } = "";
    public string? TextContent { get; set; }
    public byte[]? BinaryContent { get; set; }
    public string? Error { get; set; }
}

public class FileOperationResultDto
{
    public string RequestId { get; set; } = "";
    public bool Success { get; set; }
    public string? Message { get; set; }
}
