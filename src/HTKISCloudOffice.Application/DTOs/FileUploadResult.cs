namespace HTKISCloudOffice.Application.DTOs;

public class FileUploadResult
{
    public bool success { get; init; }
    public FileItemDto? file { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static FileUploadResult Ok(FileItemDto file) => new()
    {
        success = true,
        file = file
    };

    public static FileUploadResult Fail(string error_code, string error_message) => new()
    {
        success = false,
        error_code = error_code,
        error_message = error_message
    };
}