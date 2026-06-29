namespace HTKISCloudOffice.Application.DTOs;

public class FileOperationResult
{
    public bool success { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static FileOperationResult Ok() => new() { success = true };

    public static FileOperationResult Fail(string error_code, string error_message) => new()
    {
        success = false,
        error_code = error_code,
        error_message = error_message
    };
}