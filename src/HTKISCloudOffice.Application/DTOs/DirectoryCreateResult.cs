namespace HTKISCloudOffice.Application.DTOs;

public class DirectoryCreateResult
{
    public bool success { get; init; }
    public FileItemDto? directory { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static DirectoryCreateResult Ok(FileItemDto directory) => new()
    {
        success = true,
        directory = directory
    };

    public static DirectoryCreateResult Fail(string error_code, string error_message) => new()
    {
        success = false,
        error_code = error_code,
        error_message = error_message
    };
}