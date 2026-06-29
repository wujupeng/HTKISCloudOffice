namespace HTKISCloudOffice.Application.DTOs;

public class FileListResult
{
    public bool success { get; init; }
    public List<FileItemDto> files { get; init; } = new();
    public string current_path { get; init; } = string.Empty;
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static FileListResult Ok(List<FileItemDto> files, string current_path) => new()
    {
        success = true,
        files = files,
        current_path = current_path
    };

    public static FileListResult Fail(string error_code, string error_message) => new()
    {
        success = false,
        error_code = error_code,
        error_message = error_message
    };
}